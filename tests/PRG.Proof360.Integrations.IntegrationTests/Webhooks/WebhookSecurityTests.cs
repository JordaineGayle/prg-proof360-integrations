using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PRG.Proof360.Integrations.Api;
using PRG.Proof360.Integrations.Application.Abstractions.Persistence;
using PRG.Proof360.Integrations.Application.Contractors;
using PRG.Proof360.Integrations.Application.DependencyInjection;
using PRG.Proof360.Integrations.Application.Errors;
using PRG.Proof360.Integrations.Application.Inbox;
using PRG.Proof360.Integrations.Application.Outcomes;
using PRG.Proof360.Integrations.Core.Integration;
using PRG.Proof360.Integrations.Core.Providers.Capabilities;
using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Core.Results;
using PRG.Proof360.Integrations.Domain.Canonical;
using PRG.Proof360.Integrations.FieldFlow;
using PRG.Proof360.Integrations.FieldFlow.DependencyInjection;
using PRG.Proof360.Integrations.FieldFlow.Security;
using PRG.Proof360.Integrations.Infrastructure.Persistence;
using PRG.Proof360.Integrations.IntegrationTests.Inbound;

namespace PRG.Proof360.Integrations.IntegrationTests.Webhooks;

public sealed class WebhookSecurityTests
{
    private static string Secret => WebhookTestSecrets.Secret;
    private static string Instance => WebhookTestSecrets.Instance;

    [Fact]
    public async Task Valid_signature_is_accepted_and_stored_before_processing()
    {
        await using var fx = await WebhookFixture.CreateAsync();
        var (body, headers) = SignedEnvelope(eventId: "evt-valid-1", status: "open", version: 1);

        await fx.ScopedAsync(async sp =>
        {
            var outcome = await ReceiveAsync(sp, body, headers, "corr-valid");
            Assert.True(outcome.IsSuccess);
            Assert.IsType<ReceiveEventOutcome.Accepted>(
                ((Result<ReceiveEventOutcome, IntegrationFailure>.Succeeded)outcome).Value);

            var inbox = await sp.GetRequiredService<ConnectorDbContext>().InboxMessages.SingleAsync();
            Assert.Equal(InboxMessageStates.Pending, inbox.State);
            Assert.Equal(0, await sp.GetRequiredService<ICanonicalWriter>().CountJobsAsync());
        });
    }

    [Fact]
    public async Task Invalid_signature_returns_unauthorized_and_mutates_nothing()
    {
        await using var factory = CreateApiFactory();
        using var client = factory.CreateClient();
        var (body, headers) = SignedEnvelope(eventId: "evt-bad-sig", status: "open", version: 1);
        headers["X-FieldFlow-Signature"] = "deadbeef";

        using var response = await client.SendAsync(BuildRequest(body, headers, "corr-bad"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ConnectorDbContext>();
        Assert.Equal(0, await db.InboxMessages.CountAsync());
        Assert.Equal(0, await db.Jobs.CountAsync());
        Assert.Contains(await db.AuditEvents.ToListAsync(), a => a.Operation == "webhook.verify");
    }

    [Fact]
    public async Task Stale_timestamp_is_rejected()
    {
        await using var fx = await WebhookFixture.CreateAsync();
        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 10_000;
        var (body, headers) = SignedEnvelope(eventId: "evt-stale", status: "open", version: 1, unixSeconds: unix);

        await fx.ScopedAsync(async sp =>
        {
            var outcome = await ReceiveAsync(sp, body, headers, "corr-stale");
            Assert.True(outcome.IsFailure);
            Assert.Equal(
                FailureCodes.WebhookTimestampSkew,
                ((Result<ReceiveEventOutcome, IntegrationFailure>.Failed)outcome).Error.Code);
            Assert.Equal(0, await sp.GetRequiredService<ConnectorDbContext>().InboxMessages.CountAsync());
        });
    }

    [Fact]
    public async Task Signature_is_over_raw_bytes_not_reformatted_json()
    {
        await using var fx = await WebhookFixture.CreateAsync();
        var compact = Encoding.UTF8.GetBytes(BuildBodyJson("evt-raw", "open", 1));
        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = FieldFlowWebhookVerifier.Sign(Secret, unix, compact);
        var pretty = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            JsonSerializer.Deserialize<JsonElement>(compact),
            new JsonSerializerOptions { WriteIndented = true }));

        var headers = BaseHeaders("evt-raw", "work_order.status_changed", "1.0", "1", unix, signature);
        await fx.ScopedAsync(async sp =>
        {
            Assert.True((await ReceiveAsync(sp, pretty, headers, "corr-raw")).IsFailure);
        });
    }

    [Fact]
    public async Task Valid_duplicate_is_idempotent_with_one_canonical_effect()
    {
        await using var fx = await WebhookFixture.CreateAsync(seedContractor: true);
        var (body, headers) = SignedEnvelope(eventId: "evt-dup-1", status: "open", version: 1);

        await fx.ScopedAsync(async sp =>
        {
            Assert.True((await ReceiveAsync(sp, body, headers, "corr-dup-a")).IsSuccess);
            var second = await ReceiveAsync(sp, body, headers, "corr-dup-b");
            Assert.True(second.IsSuccess);
            Assert.IsType<ReceiveEventOutcome.Duplicate>(
                ((Result<ReceiveEventOutcome, IntegrationFailure>.Succeeded)second).Value);

            Assert.True((await sp.GetRequiredService<ProcessInboxMessageHandler>()
                .HandleAsync(Instance, CancellationToken.None)).IsSuccess);
            Assert.True((await sp.GetRequiredService<ProcessInboxMessageHandler>()
                .HandleAsync(Instance, CancellationToken.None)).IsSuccess);

            Assert.Equal(
                1,
                await sp.GetRequiredService<ConnectorDbContext>().InboxMessages
                    .CountAsync(x => x.EventId == "evt-dup-1"));
            Assert.Equal(1, await sp.GetRequiredService<ICanonicalWriter>().CountJobsAsync());
        });
    }

    [Fact]
    public async Task Concurrent_duplicate_webhooks_create_one_inbox_and_one_job()
    {
        var path = Path.Combine(Path.GetTempPath(), $"prg-webhook-{Guid.NewGuid():N}.db");
        var (body, headers) = SignedEnvelope(eventId: "evt-concurrent", status: "open", version: 1);
        try
        {
            await using (var setup = await WebhookFixture.CreateFileAsync(path, seedContractor: true))
            {
            }

            await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => ImportOnceAsync(path, body, headers)));

            await using var verify = new ConnectorDbContext(
                new DbContextOptionsBuilder<ConnectorDbContext>().UseSqlite($"Data Source={path}").Options);
            Assert.Equal(1, await verify.InboxMessages.CountAsync(x => x.EventId == "evt-concurrent"));
            Assert.Equal(1, await verify.Jobs.CountAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Newer_status_then_terminal_blocks_older_event_regression()
    {
        await using var fx = await WebhookFixture.CreateAsync(seedContractor: true);
        await fx.ScopedAsync(async sp =>
        {
            var open = SignedEnvelope(eventId: "evt-new-1", status: "open", version: 1);
            var inProgress = SignedEnvelope(eventId: "evt-new-2", status: "in_progress", version: 2);
            var done = SignedEnvelope(eventId: "evt-new-3", status: "done", version: 3);
            var regress = SignedEnvelope(eventId: "evt-old-1", status: "scheduled", version: 1);

            Assert.True((await ReceiveAsync(sp, open.Body, open.Headers, "c1")).IsSuccess);
            Assert.True((await ReceiveAsync(sp, inProgress.Body, inProgress.Headers, "c2")).IsSuccess);
            Assert.True((await ReceiveAsync(sp, done.Body, done.Headers, "c3")).IsSuccess);
            Assert.True((await ReceiveAsync(sp, regress.Body, regress.Headers, "c4")).IsSuccess);

            var process = sp.GetRequiredService<ProcessInboxMessageHandler>();
            for (var i = 0; i < 4; i++)
            {
                Assert.True((await process.HandleAsync(Instance, CancellationToken.None)).IsSuccess);
            }

            var job = await sp.GetRequiredService<ConnectorDbContext>().Jobs.SingleAsync();
            Assert.Equal(JobStatuses.Completed, job.Status);
            Assert.Contains(
                await sp.GetRequiredService<ConnectorDbContext>().AuditEvents.ToListAsync(),
                a => a.Result == "ignored_stale");
        });
    }

    [Fact]
    public async Task Equal_version_different_payload_is_version_payload_conflict()
    {
        await using var fx = await WebhookFixture.CreateAsync(seedContractor: true);
        await fx.ScopedAsync(async sp =>
        {
            var first = SignedEnvelope(eventId: "evt-eq-1", status: "open", version: 5, customerName: "Ada");
            var conflict = SignedEnvelope(eventId: "evt-eq-2", status: "open", version: 5, customerName: "Grace");

            Assert.True((await ReceiveAsync(sp, first.Body, first.Headers, "c1")).IsSuccess);
            Assert.True((await sp.GetRequiredService<ProcessInboxMessageHandler>()
                .HandleAsync(Instance, CancellationToken.None)).IsSuccess);

            Assert.True((await ReceiveAsync(sp, conflict.Body, conflict.Headers, "c2")).IsSuccess);
            var processed = await sp.GetRequiredService<ProcessInboxMessageHandler>()
                .HandleAsync(Instance, CancellationToken.None);
            Assert.True(processed.IsSuccess);
            var applied = Assert.IsType<ProcessInboxOutcome.WorkOrderApplied>(
                ((Result<ProcessInboxOutcome, IntegrationFailure>.Succeeded)processed).Value);
            Assert.IsType<ApplyWorkOrderOutcome.VersionPayloadConflict>(applied.Outcome);

            var job = await sp.GetRequiredService<ConnectorDbContext>().Jobs.SingleAsync();
            Assert.Equal("Ada", job.CustomerName);
        });
    }

    [Fact]
    public async Task Equal_version_same_payload_is_ignored_stale_without_mutation()
    {
        await using var fx = await WebhookFixture.CreateAsync(seedContractor: true);
        await fx.ScopedAsync(async sp =>
        {
            var first = SignedEnvelope(eventId: "evt-same-1", status: "open", version: 4, customerName: "Ada");
            var same = SignedEnvelope(eventId: "evt-same-2", status: "open", version: 4, customerName: "Ada");

            Assert.True((await ReceiveAsync(sp, first.Body, first.Headers, "c1")).IsSuccess);
            Assert.True((await sp.GetRequiredService<ProcessInboxMessageHandler>()
                .HandleAsync(Instance, CancellationToken.None)).IsSuccess);

            var before = await sp.GetRequiredService<ConnectorDbContext>().Jobs.AsNoTracking().SingleAsync();

            Assert.True((await ReceiveAsync(sp, same.Body, same.Headers, "c2")).IsSuccess);
            var processed = await sp.GetRequiredService<ProcessInboxMessageHandler>()
                .HandleAsync(Instance, CancellationToken.None);
            Assert.True(processed.IsSuccess);
            var applied = Assert.IsType<ProcessInboxOutcome.WorkOrderApplied>(
                ((Result<ProcessInboxOutcome, IntegrationFailure>.Succeeded)processed).Value);
            Assert.IsType<ApplyWorkOrderOutcome.IgnoredStale>(applied.Outcome);

            var after = await sp.GetRequiredService<ConnectorDbContext>().Jobs.AsNoTracking().SingleAsync();
            Assert.Equal(before.CustomerName, after.CustomerName);
            Assert.Equal(before.Status, after.Status);
            Assert.Equal(1, await sp.GetRequiredService<ConnectorDbContext>().Jobs.CountAsync());
            Assert.Contains(
                await sp.GetRequiredService<ConnectorDbContext>().AuditEvents.ToListAsync(),
                a => a.Result == "ignored_stale");
        });
    }

    [Fact]
    public async Task Unknown_event_type_reaches_dead_letter()
    {
        await using var fx = await WebhookFixture.CreateAsync();
        var (body, headers) = SignedEnvelope(
            eventId: "evt-unknown",
            status: "open",
            version: 1,
            eventType: "invoice.created");

        await fx.ScopedAsync(async sp =>
        {
            Assert.True((await ReceiveAsync(sp, body, headers, "corr-unknown")).IsSuccess);
            var processed = await sp.GetRequiredService<ProcessInboxMessageHandler>()
                .HandleAsync(Instance, CancellationToken.None);
            Assert.True(processed.IsSuccess);
            Assert.IsType<ProcessInboxOutcome.DeadLettered>(
                ((Result<ProcessInboxOutcome, IntegrationFailure>.Succeeded)processed).Value);

            var inbox = await sp.GetRequiredService<ConnectorDbContext>().InboxMessages.SingleAsync();
            Assert.Equal(InboxMessageStates.DeadLettered, inbox.State);
            Assert.Equal(0, await sp.GetRequiredService<ICanonicalWriter>().CountJobsAsync());
        });
    }

    [Fact]
    public async Task Correlation_id_persists_across_receipt_process_and_audit()
    {
        await using var fx = await WebhookFixture.CreateAsync(seedContractor: true);
        const string correlation = "corr-persist-42";
        var (body, headers) = SignedEnvelope(eventId: "evt-corr", status: "open", version: 1);

        await fx.ScopedAsync(async sp =>
        {
            Assert.True((await ReceiveAsync(sp, body, headers, correlation)).IsSuccess);
            Assert.True((await sp.GetRequiredService<ProcessInboxMessageHandler>()
                .HandleAsync(Instance, CancellationToken.None)).IsSuccess);

            var inbox = await sp.GetRequiredService<ConnectorDbContext>().InboxMessages
                .SingleAsync(x => x.EventId == "evt-corr");
            Assert.Equal(correlation, inbox.CorrelationId);
            Assert.Contains(
                await sp.GetRequiredService<ConnectorDbContext>().AuditEvents.ToListAsync(),
                a => a.CorrelationId == correlation && a.Operation == "work_order.apply");
        });
    }

    [Fact]
    public async Task Audit_contains_none_of_the_sensitive_markers()
    {
        await using var fx = await WebhookFixture.CreateAsync();
        var (body, headers) = SignedEnvelope(eventId: "evt-sensitive", status: "open", version: 1);
        headers["X-FieldFlow-Signature"] = "not-the-real-signature";

        await fx.ScopedAsync(async sp =>
        {
            _ = await ReceiveAsync(sp, body, headers, "corr-sensitive");
            var blob = JsonSerializer.Serialize(await sp.GetRequiredService<ConnectorDbContext>().AuditEvents.ToListAsync());
            Assert.DoesNotContain(Secret, blob, StringComparison.Ordinal);
            Assert.DoesNotContain("not-the-real-signature", blob, StringComparison.Ordinal);
            Assert.DoesNotContain("X-FieldFlow-Signature", blob, StringComparison.Ordinal);
            Assert.DoesNotContain("customerName", blob, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Http_endpoint_returns_202_for_valid_and_duplicate()
    {
        await using var factory = CreateApiFactory(seedContractor: true);
        using var client = factory.CreateClient();
        var (body, headers) = SignedEnvelope(eventId: "evt-http-1", status: "open", version: 1);

        using var first = await client.SendAsync(BuildRequest(body, headers, "corr-http-1"));
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);

        using var second = await client.SendAsync(BuildRequest(body, headers, "corr-http-2"));
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        var payload = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("duplicate", payload.GetProperty("status").GetString());
    }

    private static async Task<Result<ReceiveEventOutcome, IntegrationFailure>> ReceiveAsync(
        IServiceProvider sp,
        byte[] body,
        Dictionary<string, string> headers,
        string correlationId) =>
        await sp.GetRequiredService<ReceiveWebhookEventHandler>().HandleAsync(
            new ReceiveWebhookEventCommand
            {
                RawBody = body,
                SignatureHeader = headers["X-FieldFlow-Signature"],
                TimestampHeader = headers["X-FieldFlow-Timestamp"],
                ProviderInstanceHeader = headers["X-FieldFlow-Provider-Instance"],
                EventIdHeader = headers["X-FieldFlow-Event-Id"],
                EventTypeHeader = headers["X-FieldFlow-Event-Type"],
                SchemaVersionHeader = headers["X-FieldFlow-Schema-Version"],
                EntityVersionHeader = headers["X-FieldFlow-Entity-Version"],
                CorrelationId = correlationId
            },
            CancellationToken.None);

    private static (byte[] Body, Dictionary<string, string> Headers) SignedEnvelope(
        string eventId,
        string status,
        long version,
        string eventType = "work_order.status_changed",
        string customerName = "Ada Fixture",
        long? unixSeconds = null)
    {
        var unix = unixSeconds ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var body = Encoding.UTF8.GetBytes(BuildBodyJson(eventId, status, version, eventType, customerName));
        var signature = FieldFlowWebhookVerifier.Sign(Secret, unix, body);
        return (body, BaseHeaders(eventId, eventType, "1.0", version.ToString(), unix, signature));
    }

    private static string BuildBodyJson(
        string eventId,
        string status,
        long version,
        string eventType = "work_order.status_changed",
        string customerName = "Ada Fixture") =>
        "{" +
        $"\"eventId\":\"{eventId}\"," +
        $"\"eventType\":\"{eventType}\"," +
        "\"schemaVersion\":\"1.0\"," +
        $"\"entityVersion\":{version}," +
        "\"occurredAt\":\"2026-07-17T14:05:00+00:00\"," +
        $"\"providerInstanceId\":\"{Instance}\"," +
        "\"data\":{" +
        "\"workOrderId\":\"wo-2001\"," +
        "\"contractorId\":\"ctr-1001\"," +
        $"\"status\":\"{status}\"," +
        $"\"entityVersion\":{version}," +
        $"\"customerName\":\"{customerName}\"," +
        "\"addressStreet\":\"100 Mock Street\"," +
        "\"addressCity\":\"Calgary\"," +
        "\"serviceType\":\"plumbing\"" +
        "}}";

    private static Dictionary<string, string> BaseHeaders(
        string eventId,
        string eventType,
        string schema,
        string entityVersion,
        long unix,
        string signature) =>
        new()
        {
            ["X-FieldFlow-Provider-Instance"] = Instance,
            ["X-FieldFlow-Event-Id"] = eventId,
            ["X-FieldFlow-Event-Type"] = eventType,
            ["X-FieldFlow-Schema-Version"] = schema,
            ["X-FieldFlow-Entity-Version"] = entityVersion,
            ["X-FieldFlow-Timestamp"] = unix.ToString(),
            ["X-FieldFlow-Signature"] = signature
        };

    private static HttpRequestMessage BuildRequest(byte[] body, Dictionary<string, string> headers, string correlationId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/events")
        {
            Content = new ByteArrayContent(body)
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        foreach (var (key, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(key, value);
        }

        request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
        return request;
    }

    private static WebApplicationFactory<ApiAssemblyMarker> CreateApiFactory(bool seedContractor = false)
    {
        var path = Path.Combine(Path.GetTempPath(), $"prg-api-webhook-{Guid.NewGuid():N}.db");
        var factory = new WebApplicationFactory<ApiAssemblyMarker>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectorPersistence:ConnectionString", $"Data Source={path}");
            builder.UseSetting("ConnectorPersistence:ApplyMigrationsOnStartup", "true");
            builder.UseSetting("FieldFlow:WebhookHmacSecret", Secret);
            builder.UseSetting("FieldFlow:ProviderInstanceId", Instance);
            builder.UseSetting("FieldFlow:ApiKey", "test-key");
            builder.UseSetting("FieldFlow:BaseUrl", "http://localhost:5210");
            builder.UseSetting("InboundSync:PollingEnabled", "false");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IContractorSnapshotSource>();
                services.RemoveAll<IWorkOrderSnapshotSource>();
                var contractors = seedContractor
                    ? new[] { InboundFixtures.Contractor() }
                    : Array.Empty<ContractorSnapshot>();
                services.AddSingleton<IContractorSnapshotSource>(new FixedContractorSource(contractors));
                services.AddSingleton<IWorkOrderSnapshotSource>(new EmptyWorkOrderSource());
            });
        });

        if (seedContractor)
        {
            using var scope = factory.Services.CreateScope();
            Assert.True(scope.ServiceProvider.GetRequiredService<ImportContractorsHandler>()
                .HandleAsync(CancellationToken.None).GetAwaiter().GetResult().IsSuccess);
        }

        return factory;
    }

    private static async Task ImportOnceAsync(string path, byte[] body, Dictionary<string, string> headers)
    {
        var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync();
        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddApplication();
            services.Configure<FieldFlowOptions>(o =>
            {
                o.BaseUrl = "http://localhost:5210";
                o.WebhookHmacSecret = Secret;
                o.ProviderInstanceId = Instance;
            });
            services.AddFieldFlow();
            services.AddDbContext<ConnectorDbContext>(o => o.UseSqlite(connection));
            services.AddScoped<IConnectorUnitOfWork, ConnectorUnitOfWork>();
            services.AddScoped<ICanonicalWriter, CanonicalWriter>();
            services.AddScoped<IIntegrationStore, IntegrationStore>();
            services.AddSingleton<IPersistenceExceptionClassifier, PersistenceExceptionClassifier>();
            services.RemoveAll<IContractorSnapshotSource>();
            services.RemoveAll<IWorkOrderSnapshotSource>();
            services.AddSingleton<IContractorSnapshotSource>(new FixedContractorSource([InboundFixtures.Contractor()]));
            services.AddSingleton<IWorkOrderSnapshotSource>(new EmptyWorkOrderSource());

            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            Assert.True((await ReceiveAsync(scope.ServiceProvider, body, headers, Guid.NewGuid().ToString("N"))).IsSuccess);
            await scope.ServiceProvider.GetRequiredService<ProcessInboxMessageHandler>()
                .HandleAsync(Instance, CancellationToken.None);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }
}
