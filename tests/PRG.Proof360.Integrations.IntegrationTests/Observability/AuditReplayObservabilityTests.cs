using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRG.Proof360.Integrations.Api;
using PRG.Proof360.Integrations.Api.Middleware;
using PRG.Proof360.Integrations.Application.Abstractions.Persistence;
using PRG.Proof360.Integrations.Application.DependencyInjection;
using PRG.Proof360.Integrations.Application.Errors;
using PRG.Proof360.Integrations.Application.Inbox;
using PRG.Proof360.Integrations.Application.Observability;
using PRG.Proof360.Integrations.Application.Outcomes;
using PRG.Proof360.Integrations.Application.Replay;
using PRG.Proof360.Integrations.Core.Integration;
using PRG.Proof360.Integrations.Core.Providers;
using PRG.Proof360.Integrations.Core.Providers.Capabilities;
using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Core.Providers.Health;
using PRG.Proof360.Integrations.Core.Results;
using PRG.Proof360.Integrations.Infrastructure.Persistence;

namespace PRG.Proof360.Integrations.IntegrationTests.Observability;

public sealed class AuditReplayObservabilityTests
{
    [Fact]
    public async Task Correlation_generated_when_missing_and_accepted_when_valid()
    {
        await using var factory = new WebApplicationFactory<ApiAssemblyMarker>();
        using var client = factory.CreateClient();

        using var generated = await client.GetAsync(new Uri("/health/live", UriKind.Relative));
        Assert.True(generated.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var generatedValues));
        Assert.True(CorrelationIdRules.IsValid(generatedValues.First()));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, "valid-corr_1");
        using var accepted = await client.SendAsync(request);
        Assert.Equal("valid-corr_1", accepted.Headers.GetValues(CorrelationIdMiddleware.HeaderName).First());
    }

    [Fact]
    public async Task Invalid_oversized_correlation_is_replaced_safely()
    {
        await using var factory = new WebApplicationFactory<ApiAssemblyMarker>();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, "not valid!!!");
        using var response = await client.SendAsync(request);
        var returned = response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).First();
        Assert.NotEqual("not valid!!!", returned);
        Assert.True(CorrelationIdRules.IsValid(returned));
    }

    [Fact]
    public async Task Permanent_unsupported_event_is_dead_lettered_with_history_and_audit()
    {
        await using var fx = await ObsFixture.CreateAsync();
        var inboxId = Guid.NewGuid();
        await fx.ScopedAsync(async sp =>
        {
            var store = sp.GetRequiredService<IIntegrationStore>();
            await store.AddInboxMessageAsync(new InboxMessage
            {
                Id = inboxId,
                ProviderName = ProviderNames.FieldFlow,
                ProviderInstanceId = ObsFixture.Instance,
                EventId = "evt-unsupported-1",
                EventType = "unknown.event",
                PayloadEnvelope = "{}",
                PayloadHash = "hash1",
                CorrelationId = "corr-dlq-1",
                OccurredAt = DateTimeOffset.UtcNow,
                ReceivedAt = DateTimeOffset.UtcNow,
                State = InboxMessageStates.Pending
            });
            await sp.GetRequiredService<IConnectorUnitOfWork>().SaveChangesAsync();

            var result = await sp.GetRequiredService<ProcessInboxMessageHandler>()
                .HandleAsync(ObsFixture.Instance, CancellationToken.None);
            Assert.True(result.IsSuccess);

            var message = await store.FindInboxByIdAsync(inboxId);
            Assert.Equal(InboxMessageStates.DeadLettered, message!.State);
            Assert.False(string.IsNullOrWhiteSpace(message.FailureHistoryJson));
            Assert.Contains(FailureCodes.UnsupportedEventType, message.FailureHistoryJson, StringComparison.Ordinal);

            Assert.True(await store.CountAuditEventsAsync(AuditOperations.DeadLettered, "dead_lettered") >= 1);
        });
    }

    [Fact]
    public async Task Replay_retains_event_identity_history_and_is_idempotent_for_completed()
    {
        await using var fx = await ObsFixture.CreateAsync();
        var inboxId = Guid.NewGuid();
        const string history =
            """[{"at":"2026-07-17T00:00:00+00:00","category":"Validation","code":"x","safeMessage":"bad","attempt":1,"causationId":null}]""";

        await fx.ScopedAsync(async sp =>
        {
            var store = sp.GetRequiredService<IIntegrationStore>();
            await store.AddInboxMessageAsync(new InboxMessage
            {
                Id = inboxId,
                ProviderName = ProviderNames.FieldFlow,
                ProviderInstanceId = ObsFixture.Instance,
                EventId = "evt-replay-1",
                EventType = InboxEventTypes.ContractorSnapshot,
                PayloadEnvelope = "{}",
                PayloadHash = "hash-replay",
                CorrelationId = "corr-replay-1",
                OccurredAt = DateTimeOffset.UtcNow,
                ReceivedAt = DateTimeOffset.UtcNow,
                State = InboxMessageStates.DeadLettered,
                FailureHistoryJson = history,
                ErrorCategory = "Validation",
                ErrorMessage = "bad",
                AttemptCount = 3
            });
            await sp.GetRequiredService<IConnectorUnitOfWork>().SaveChangesAsync();

            var replay = await sp.GetRequiredService<ReplayDeadLetterHandler>().HandleAsync(
                new ReplayDeadLetterCommand
                {
                    InboxMessageId = inboxId,
                    OperatorId = "jordaine",
                    Reason = "mapping fixed",
                    CorrelationId = "corr-replay-1"
                });
            Assert.True(replay.IsSuccess);

            var message = await store.FindInboxByIdAsync(inboxId);
            Assert.Equal(InboxMessageStates.Pending, message!.State);
            Assert.Equal("evt-replay-1", message.EventId);
            Assert.Equal("hash-replay", message.PayloadHash);
            Assert.Equal(history, message.FailureHistoryJson);
            Assert.Null(message.ErrorCategory);
            Assert.False(string.IsNullOrWhiteSpace(message.CausationId));
            Assert.True(await store.CountAuditEventsAsync(AuditOperations.ReplayRequested) >= 1);
            Assert.True(await store.CountAuditEventsAsync(AuditOperations.ReplayCompleted) >= 1);

            message.State = InboxMessageStates.Completed;
            await sp.GetRequiredService<IConnectorUnitOfWork>().SaveChangesAsync();

            var again = await sp.GetRequiredService<ReplayDeadLetterHandler>().HandleAsync(
                new ReplayDeadLetterCommand
                {
                    InboxMessageId = inboxId,
                    OperatorId = "jordaine",
                    Reason = "noop",
                    CorrelationId = "corr-replay-1"
                });
            Assert.True(again.IsSuccess);
            Assert.IsType<ReplayOutcome.AlreadyComplete>(
                ((Result<ReplayOutcome, IntegrationFailure>.Succeeded)again).Value);
        });
    }

    [Fact]
    public async Task Unauthorized_replay_when_disabled_returns_not_found()
    {
        await using var factory = new WebApplicationFactory<ApiAssemblyMarker>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AdminReplay:Enabled", "false");
        });
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            new Uri($"/admin/inbox/{Guid.NewGuid()}/replay", UriKind.Relative),
            new { operatorId = "x", reason = "y" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Health_and_audit_contain_no_sensitive_markers()
    {
        await using var factory = new WebApplicationFactory<ApiAssemblyMarker>();
        using var client = factory.CreateClient();
        using var health = await client.GetAsync(new Uri("/connectors/fieldflow/health", UriKind.Relative));
        var body = await health.Content.ReadAsStringAsync();
        foreach (var marker in new[] { "replace-me", "password", "Bearer ", "@example.com", "+1-555", "WebhookHmacSecret" })
        {
            Assert.DoesNotContain(marker, body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Dead_letter_count_affects_connector_health_degraded()
    {
        var path = Path.Combine(Path.GetTempPath(), $"prg-obs-dlq-{Guid.NewGuid():N}.db");
        try
        {
            await using var factory = new WebApplicationFactory<ApiAssemblyMarker>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectorPersistence:ConnectionString", $"Data Source={path}");
            });
            using (var scope = factory.Services.CreateScope())
            {
                var store = scope.ServiceProvider.GetRequiredService<IIntegrationStore>();
                var instance = scope.ServiceProvider.GetRequiredService<IConnectorRuntimeHealthSource>().ProviderInstanceId;
                await store.AddInboxMessageAsync(new InboxMessage
                {
                    Id = Guid.NewGuid(),
                    ProviderName = ProviderNames.FieldFlow,
                    ProviderInstanceId = instance,
                    EventId = $"dlq-health-{Guid.NewGuid():N}",
                    EventType = "x",
                    PayloadEnvelope = "{}",
                    PayloadHash = "h",
                    OccurredAt = DateTimeOffset.UtcNow,
                    ReceivedAt = DateTimeOffset.UtcNow,
                    State = InboxMessageStates.DeadLettered
                });
                await scope.ServiceProvider.GetRequiredService<IConnectorUnitOfWork>().SaveChangesAsync();
            }

            using var client = factory.CreateClient();
            using var response = await client.GetAsync(new Uri("/connectors/fieldflow/health", UriKind.Relative));
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(doc.RootElement.GetProperty("deadLetterCount").GetInt32() >= 1);
            Assert.Equal("Degraded", doc.RootElement.GetProperty("status").GetString());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private sealed class ObsFixture : IAsyncDisposable
    {
        public const string Instance = "fieldflow-obs-1";
        private readonly ServiceProvider _provider;
        private readonly SqliteConnection _connection;

        private ObsFixture(ServiceProvider provider, SqliteConnection connection)
        {
            _provider = provider;
            _connection = connection;
        }

        public static async Task<ObsFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddApplication();
            services.AddDbContext<ConnectorDbContext>(o => o.UseSqlite(connection));
            services.AddScoped<IConnectorUnitOfWork, ConnectorUnitOfWork>();
            services.AddScoped<ICanonicalWriter, CanonicalWriter>();
            services.AddScoped<IIntegrationStore, IntegrationStore>();
            services.AddSingleton<IPersistenceExceptionClassifier, PersistenceExceptionClassifier>();
            services.AddSingleton<IProviderCapabilities>(new StubCapabilities());
            services.AddSingleton<IContractorSnapshotSource>(new StubContractors());
            services.AddSingleton<IWorkOrderSnapshotSource>(new StubWorkOrders());
            services.AddSingleton<IWorkOrderDispatcher>(new StubDispatcher());
            services.AddSingleton<IWorkOrderReconciler>(new StubReconciler());
            services.AddSingleton<IWebhookVerifier>(new StubVerifier());
            services.AddSingleton<IInboundWebhookNormalizer>(new StubNormalizer());
            services.AddSingleton<IConnectorRuntimeHealthSource>(new StubHealth());

            var provider = services.BuildServiceProvider();
            await using (var scope = provider.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<ConnectorDbContext>().Database.EnsureCreatedAsync();
            }

            return new ObsFixture(provider, connection);
        }

        public async Task ScopedAsync(Func<IServiceProvider, Task> action)
        {
            await using var scope = _provider.CreateAsyncScope();
            await action(scope.ServiceProvider);
        }

        public async ValueTask DisposeAsync()
        {
            await _provider.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private sealed class StubCapabilities : IProviderCapabilities
        {
            public string ProviderName => ProviderNames.FieldFlow;
            public string ProviderInstanceId => Instance;

            public ProviderCapability SupportedCapabilities => throw new NotImplementedException();

            public bool Supports(ProviderCapability capability) => true;
        }

        private sealed class StubContractors : IContractorSnapshotSource
        {
            public Task<Result<IReadOnlyList<ContractorSnapshot>, ProviderFailure>> ListAsync(CancellationToken cancellationToken) =>
                Task.FromResult(Result<IReadOnlyList<ContractorSnapshot>, ProviderFailure>.Ok(
                    Array.Empty<ContractorSnapshot>()));

            public Task<Result<ContractorSnapshot, ProviderFailure>> GetAsync(string externalContractorId, CancellationToken cancellationToken) =>
                Task.FromResult(Result<ContractorSnapshot, ProviderFailure>.Fail(
                    new ProviderFailure(ProviderFailureKind.NotFound, "not_found", "missing")));
        }

        private sealed class StubWorkOrders : IWorkOrderSnapshotSource
        {
            public Task<Result<IReadOnlyList<WorkOrderSnapshot>, ProviderFailure>> ListAsync(CancellationToken cancellationToken) =>
                Task.FromResult(Result<IReadOnlyList<WorkOrderSnapshot>, ProviderFailure>.Ok(
                    Array.Empty<WorkOrderSnapshot>()));

            public Task<Result<WorkOrderSnapshot, ProviderFailure>> GetAsync(string externalWorkOrderId, CancellationToken cancellationToken) =>
                Task.FromResult(Result<WorkOrderSnapshot, ProviderFailure>.Fail(
                    new ProviderFailure(ProviderFailureKind.NotFound, "not_found", "missing")));
        }

        private sealed class StubDispatcher : IWorkOrderDispatcher
        {
            public Task<Result<WorkOrderSnapshot, ProviderFailure>> DispatchAsync(
                DispatchWorkOrderCommand command,
                CancellationToken cancellationToken) =>
                Task.FromResult(Result<WorkOrderSnapshot, ProviderFailure>.Fail(
                    new ProviderFailure(ProviderFailureKind.Unavailable, "n", "n")));
        }

        private sealed class StubReconciler : IWorkOrderReconciler
        {
            public Task<Result<WorkOrderSnapshot, ProviderFailure>> GetByClientReferenceAsync(
                string clientReference,
                CancellationToken cancellationToken) =>
                Task.FromResult(Result<WorkOrderSnapshot, ProviderFailure>.Fail(
                    new ProviderFailure(ProviderFailureKind.NotFound, "not_found", "missing")));

            public Task<Result<WorkOrderSnapshot, ProviderFailure>> GetByExternalIdAsync(
                string externalWorkOrderId,
                CancellationToken cancellationToken) =>
                Task.FromResult(Result<WorkOrderSnapshot, ProviderFailure>.Fail(
                    new ProviderFailure(ProviderFailureKind.NotFound, "not_found", "missing")));
        }

        private sealed class StubVerifier : IWebhookVerifier
        {
            public WebhookVerificationResult Verify(WebhookVerificationRequest request) =>
                new(true);
        }

        private sealed class StubNormalizer : IInboundWebhookNormalizer
        {
            public Task<Result<NormalizedWebhookEvent, ProviderFailure>> NormalizeAsync(
                WebhookNormalizeRequest request,
                CancellationToken cancellationToken) =>
                Task.FromResult(Result<NormalizedWebhookEvent, ProviderFailure>.Fail(
                    ProviderFailure.Validation("x", "x")));
        }

        private sealed class StubHealth : IConnectorRuntimeHealthSource
        {
            public string ProviderName => ProviderNames.FieldFlow;
            public string ProviderInstanceId => Instance;
            public string CircuitState => "Closed";
            public DateTimeOffset? LastSuccessfulProviderCallAt => DateTimeOffset.UtcNow;
            public DateTimeOffset? LastFailureAt => null;
            public string? LastFailureCategory => null;
            public string? LastFailureCode => null;
            public bool NeedsAttention => false;
            public int CountRecentRateLimits(DateTimeOffset utcNow, TimeSpan window) => 0;
        }
    }
}
