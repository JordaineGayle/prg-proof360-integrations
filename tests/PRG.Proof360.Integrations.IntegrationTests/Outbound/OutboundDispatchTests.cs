using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRG.Proof360.Integrations.Application.Abstractions.Persistence;
using PRG.Proof360.Integrations.Application.DependencyInjection;
using PRG.Proof360.Integrations.Application.Dispatch;
using PRG.Proof360.Integrations.Application.Errors;
using PRG.Proof360.Integrations.Application.Mapping;
using PRG.Proof360.Integrations.Core.Integration;
using PRG.Proof360.Integrations.Core.Providers;
using PRG.Proof360.Integrations.Core.Providers.Capabilities;
using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Core.Results;
using PRG.Proof360.Integrations.Domain.Canonical;
using PRG.Proof360.Integrations.Infrastructure.Persistence;

namespace PRG.Proof360.Integrations.IntegrationTests.Outbound;

public sealed class OutboundDispatchTests
{
    private const string Instance = "fieldflow-test-1";

    [Fact]
    public async Task Eligible_job_creates_one_outbox_without_http()
    {
        var dispatcher = new RecordingDispatcher();
        await using var fx = await OutboundFixture.CreateAsync(dispatcher);
        var jobId = await fx.SeedQualifiedAsync();

        await fx.ScopedAsync(async sp =>
        {
            var queued = await sp.GetRequiredService<QueueJobDispatchHandler>()
                .HandleAsync(jobId, "corr-q1", CancellationToken.None);
            Assert.True(queued.IsSuccess);
            Assert.IsType<QueueDispatchOutcome.Queued>(
                ((Result<QueueDispatchOutcome, IntegrationFailure>.Succeeded)queued).Value);
            Assert.Equal(0, dispatcher.DispatchCount);
            Assert.Equal(1, await sp.GetRequiredService<IIntegrationStore>()
                .CountOutboxMessagesAsync(Instance, OutboxOperationTypes.DispatchWorkOrder));
            Assert.Equal(
                1,
                await sp.GetRequiredService<IIntegrationStore>().CountAuditEventsAsync("dispatch.requested"));
        });
    }

    [Fact]
    public async Task Ineligible_status_is_rejected()
    {
        await using var fx = await OutboundFixture.CreateAsync(new RecordingDispatcher());
        var jobId = await fx.SeedQualifiedAsync();
        await fx.ScopedAsync(async sp =>
        {
            var job = await sp.GetRequiredService<ICanonicalWriter>().FindJobAsync(jobId);
            job!.Status = JobStatuses.Scheduled;
            await sp.GetRequiredService<IConnectorUnitOfWork>().SaveChangesAsync();

            var result = await sp.GetRequiredService<QueueJobDispatchHandler>()
                .HandleAsync(jobId, "c", CancellationToken.None);
            Assert.True(result.IsFailure);
            Assert.Equal(
                FailureCodes.JobNotQualified,
                ((Result<QueueDispatchOutcome, IntegrationFailure>.Failed)result).Error.Code);
        });
    }

    [Fact]
    public async Task Unapproved_vendor_is_rejected()
    {
        await using var fx = await OutboundFixture.CreateAsync(new RecordingDispatcher());
        var jobId = await fx.SeedQualifiedAsync(vendorStatus: VendorStatuses.PendingReview);
        await fx.ScopedAsync(async sp =>
        {
            var result = await sp.GetRequiredService<QueueJobDispatchHandler>()
                .HandleAsync(jobId, "c", CancellationToken.None);
            Assert.True(result.IsFailure);
            Assert.Equal(
                FailureCodes.ApprovalRequired,
                ((Result<QueueDispatchOutcome, IntegrationFailure>.Failed)result).Error.Code);
        });
    }

    [Fact]
    public async Task Concurrent_dispatch_requests_create_one_outbox()
    {
        var path = Path.Combine(Path.GetTempPath(), $"prg-outbox-{Guid.NewGuid():N}.db");
        try
        {
            Guid jobId;
            await using (var setup = await OutboundFixture.CreateFileAsync(path, new RecordingDispatcher()))
            {
                jobId = await setup.SeedQualifiedAsync();
            }

            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => QueueOnceAsync(path, jobId)));

            await using var verify = new ConnectorDbContext(
                new DbContextOptionsBuilder<ConnectorDbContext>().UseSqlite($"Data Source={path}").Options);
            Assert.Equal(1, await verify.OutboxMessages.CountAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Successful_provider_response_links_and_dispatches_atomically()
    {
        var dispatcher = new RecordingDispatcher(succeedWithId: "wo-out-1");
        await using var fx = await OutboundFixture.CreateAsync(dispatcher);
        var jobId = await fx.SeedQualifiedAsync();

        await fx.ScopedAsync(async sp =>
        {
            Assert.True((await sp.GetRequiredService<QueueJobDispatchHandler>()
                .HandleAsync(jobId, "corr-ok", CancellationToken.None)).IsSuccess);
            var processed = await sp.GetRequiredService<ProcessOutboxMessageHandler>()
                .HandleAsync(Instance, CancellationToken.None);
            Assert.True(processed.IsSuccess);
            Assert.Equal(1, dispatcher.DispatchCount);

            var job = await sp.GetRequiredService<ICanonicalWriter>().FindJobAsync(jobId);
            Assert.Equal(JobStatuses.Dispatched, job!.Status);
            var link = await sp.GetRequiredService<IIntegrationStore>()
                .FindIdentityByCanonicalAsync(Instance, CanonicalEntityTypes.Job, jobId);
            Assert.Equal("wo-out-1", link!.ExternalId);
            var outbox = await sp.GetRequiredService<ConnectorDbContext>().OutboxMessages.SingleAsync();
            Assert.Equal(OutboxMessageStates.Completed, outbox.State);
        });
    }

    [Fact]
    public async Task Failure_before_local_completion_recovers_without_second_post()
    {
        var dispatcher = new RecordingDispatcher(succeedWithId: "wo-recover");
        await using var fx = await OutboundFixture.CreateAsync(dispatcher);
        var jobId = await fx.SeedQualifiedAsync();

        await fx.ScopedAsync(async sp =>
        {
            Assert.True((await sp.GetRequiredService<QueueJobDispatchHandler>()
                .HandleAsync(jobId, "corr-recover", CancellationToken.None)).IsSuccess);

            var outbox = await sp.GetRequiredService<ConnectorDbContext>().OutboxMessages.SingleAsync();
            outbox.ResultReference = "wo-recover";
            outbox.State = OutboxMessageStates.Pending;
            outbox.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await sp.GetRequiredService<IConnectorUnitOfWork>().SaveChangesAsync();

            var processed = await sp.GetRequiredService<ProcessOutboxMessageHandler>()
                .HandleAsync(Instance, CancellationToken.None);
            Assert.True(processed.IsSuccess);
            Assert.Equal(0, dispatcher.DispatchCount);

            var job = await sp.GetRequiredService<ICanonicalWriter>().FindJobAsync(jobId);
            Assert.Equal(JobStatuses.Dispatched, job!.Status);
        });
    }

    [Fact]
    public async Task Ambiguous_post_reconciles_without_duplicate_create()
    {
        var dispatcher = new RecordingDispatcher(ambiguous: true);
        var reconciler = new RecordingReconciler(
            Result<WorkOrderSnapshot, ProviderFailure>.Ok(new WorkOrderSnapshot
            {
                ProviderName = ProviderNames.FieldFlow,
                ProviderInstanceId = Instance,
                ExternalWorkOrderId = "wo-amb-1",
                ClientReference = null,
                ProviderStatus = "open",
                EntityVersion = 1
            }));
        await using var fx = await OutboundFixture.CreateAsync(dispatcher, reconciler);
        var jobId = await fx.SeedQualifiedAsync();
        reconciler.SetClientReference(jobId.ToString("D"));

        await fx.ScopedAsync(async sp =>
        {
            Assert.True((await sp.GetRequiredService<QueueJobDispatchHandler>()
                .HandleAsync(jobId, "corr-amb", CancellationToken.None)).IsSuccess);
            Assert.True((await sp.GetRequiredService<ProcessOutboxMessageHandler>()
                .HandleAsync(Instance, CancellationToken.None)).IsSuccess);

            Assert.Equal(1, dispatcher.DispatchCount);
            Assert.Equal(1, reconciler.LookupCount);
            Assert.Equal(JobStatuses.Dispatched, (await sp.GetRequiredService<ICanonicalWriter>().FindJobAsync(jobId))!.Status);
            Assert.Equal(1, await sp.GetRequiredService<ConnectorDbContext>().OutboxMessages.CountAsync());
        });
    }

    [Fact]
    public async Task Retry_uses_same_idempotency_key_and_correlation()
    {
        var dispatcher = new RecordingDispatcher(failTransientTimes: 1, succeedWithId: "wo-retry");
        await using var fx = await OutboundFixture.CreateAsync(dispatcher);
        var jobId = await fx.SeedQualifiedAsync();
        var key = DispatchIdempotencyKeys.ForJob(Instance, jobId);

        await fx.ScopedAsync(async sp =>
        {
            Assert.True((await sp.GetRequiredService<QueueJobDispatchHandler>()
                .HandleAsync(jobId, "corr-retry", CancellationToken.None)).IsSuccess);

            var first = await sp.GetRequiredService<ProcessOutboxMessageHandler>()
                .HandleAsync(Instance, CancellationToken.None);
            Assert.True(first.IsSuccess);
            Assert.IsType<ProcessOutboxOutcome.RetryScheduled>(
                ((Result<ProcessOutboxOutcome, IntegrationFailure>.Succeeded)first).Value);

            var outbox = await sp.GetRequiredService<ConnectorDbContext>().OutboxMessages.SingleAsync();
            Assert.Equal(key, outbox.IdempotencyKey);
            outbox.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await sp.GetRequiredService<IConnectorUnitOfWork>().SaveChangesAsync();

            Assert.True((await sp.GetRequiredService<ProcessOutboxMessageHandler>()
                .HandleAsync(Instance, CancellationToken.None)).IsSuccess);
            Assert.Equal(2, dispatcher.DispatchCount);
            Assert.All(dispatcher.Keys, k => Assert.Equal(key, k));
            Assert.Contains(
                await sp.GetRequiredService<ConnectorDbContext>().AuditEvents.ToListAsync(),
                a => a.CorrelationId == "corr-retry" && a.Operation == "dispatch.completed");
        });
    }

    [Fact]
    public async Task Same_key_with_changed_payload_is_conflict()
    {
        await using var fx = await OutboundFixture.CreateAsync(new RecordingDispatcher());
        var jobId = await fx.SeedQualifiedAsync();

        await fx.ScopedAsync(async sp =>
        {
            Assert.True((await sp.GetRequiredService<QueueJobDispatchHandler>()
                .HandleAsync(jobId, "c1", CancellationToken.None)).IsSuccess);

            var job = await sp.GetRequiredService<ICanonicalWriter>().FindJobAsync(jobId);
            job!.CustomerName = "Changed Customer";
            await sp.GetRequiredService<IConnectorUnitOfWork>().SaveChangesAsync();

            var second = await sp.GetRequiredService<QueueJobDispatchHandler>()
                .HandleAsync(jobId, "c2", CancellationToken.None);
            Assert.True(second.IsFailure);
            Assert.Equal(
                FailureCodes.IdempotencyKeyConflict,
                ((Result<QueueDispatchOutcome, IntegrationFailure>.Failed)second).Error.Code);
        });
    }

    [Fact]
    public async Task Provider_error_does_not_partially_complete()
    {
        var dispatcher = new RecordingDispatcher(failPermanent: true);
        await using var fx = await OutboundFixture.CreateAsync(dispatcher);
        var jobId = await fx.SeedQualifiedAsync();

        await fx.ScopedAsync(async sp =>
        {
            Assert.True((await sp.GetRequiredService<QueueJobDispatchHandler>()
                .HandleAsync(jobId, "c", CancellationToken.None)).IsSuccess);
            Assert.True((await sp.GetRequiredService<ProcessOutboxMessageHandler>()
                .HandleAsync(Instance, CancellationToken.None)).IsSuccess);

            var job = await sp.GetRequiredService<ICanonicalWriter>().FindJobAsync(jobId);
            Assert.Equal(JobStatuses.Qualified, job!.Status);
            Assert.Null(await sp.GetRequiredService<IIntegrationStore>()
                .FindIdentityByCanonicalAsync(Instance, CanonicalEntityTypes.Job, jobId));
            var outbox = await sp.GetRequiredService<ConnectorDbContext>().OutboxMessages.SingleAsync();
            Assert.NotEqual(OutboxMessageStates.Completed, outbox.State);
        });
    }

    [Fact]
    public async Task Provider_response_does_not_overwrite_proof360_owned_fields()
    {
        var dispatcher = new RecordingDispatcher(
            succeedWithId: "wo-echo",
            customerNameEcho: "Provider Should Not Win");
        await using var fx = await OutboundFixture.CreateAsync(dispatcher);
        var jobId = await fx.SeedQualifiedAsync(customerName: "Proof360 Customer");

        await fx.ScopedAsync(async sp =>
        {
            Assert.True((await sp.GetRequiredService<QueueJobDispatchHandler>()
                .HandleAsync(jobId, "c", CancellationToken.None)).IsSuccess);
            Assert.True((await sp.GetRequiredService<ProcessOutboxMessageHandler>()
                .HandleAsync(Instance, CancellationToken.None)).IsSuccess);

            var job = await sp.GetRequiredService<ICanonicalWriter>().FindJobAsync(jobId);
            Assert.Equal("Proof360 Customer", job!.CustomerName);
            Assert.Equal(JobSources.Proof360, job.Source);
        });
    }

    private static async Task QueueOnceAsync(string path, Guid jobId)
    {
        var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync();
        try
        {
            var services = OutboundFixture.BuildServices(connection, new RecordingDispatcher());
            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<QueueJobDispatchHandler>()
                .HandleAsync(jobId, Guid.NewGuid().ToString("N"), CancellationToken.None);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }
}

internal sealed class OutboundFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private OutboundFixture(SqliteConnection connection, ServiceProvider services)
    {
        _connection = connection;
        Services = services;
    }

    public ServiceProvider Services { get; }

    public static Task<OutboundFixture> CreateAsync(
        RecordingDispatcher dispatcher,
        RecordingReconciler? reconciler = null) =>
        CreateCoreAsync("Data Source=:memory:", dispatcher, reconciler);

    public static Task<OutboundFixture> CreateFileAsync(
        string path,
        RecordingDispatcher dispatcher) =>
        CreateCoreAsync($"Data Source={path}", dispatcher, null);

    public static ServiceCollection BuildServices(
        SqliteConnection connection,
        RecordingDispatcher dispatcher,
        RecordingReconciler? reconciler = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.Configure<OutboundDispatchOptions>(o =>
        {
            o.MaxProcessBatch = 20;
            o.MaxAttempts = 8;
        });
        services.AddDbContext<ConnectorDbContext>(o => o.UseSqlite(connection));
        services.AddScoped<IConnectorUnitOfWork, ConnectorUnitOfWork>();
        services.AddScoped<ICanonicalWriter, CanonicalWriter>();
        services.AddScoped<IIntegrationStore, IntegrationStore>();
        services.AddSingleton<IPersistenceExceptionClassifier, PersistenceExceptionClassifier>();
        services.AddSingleton<IProviderCapabilities>(new FakeCaps());
        services.AddSingleton<IWorkOrderDispatcher>(dispatcher);
        services.AddSingleton<IWorkOrderReconciler>(reconciler ?? new RecordingReconciler(
            Result<WorkOrderSnapshot, ProviderFailure>.Fail(
                new ProviderFailure(ProviderFailureKind.NotFound, "not_found", "missing"))));
        services.AddSingleton<IContractorSnapshotSource>(new EmptyContractors());
        services.AddSingleton<IWorkOrderSnapshotSource>(new EmptyWorkOrders());
        services.AddSingleton<IWebhookVerifier>(new NoopVerifier());
        services.AddSingleton<IInboundWebhookNormalizer>(new NoopNormalizer());
        return services;
    }

    private static async Task<OutboundFixture> CreateCoreAsync(
        string connectionString,
        RecordingDispatcher dispatcher,
        RecordingReconciler? reconciler)
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        var services = BuildServices(connection, dispatcher, reconciler);
        var provider = services.BuildServiceProvider();
        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ConnectorDbContext>().Database.EnsureCreatedAsync();
        }

        return new OutboundFixture(connection, provider);
    }

    public async Task<Guid> SeedQualifiedAsync(
        string vendorStatus = VendorStatuses.Approved,
        string customerName = "Ada Fixture")
    {
        return await ScopedAsync(async sp =>
        {
            var vendorId = Guid.NewGuid();
            var jobId = Guid.NewGuid();
            var canonical = sp.GetRequiredService<ICanonicalWriter>();
            var store = sp.GetRequiredService<IIntegrationStore>();
            await canonical.AddVendorAsync(
                new Vendor
                {
                    VendorId = vendorId,
                    Status = vendorStatus,
                    CreatedAt = DateTimeOffset.UtcNow,
                    Rationale = "test"
                });
            await canonical.AddJobAsync(
                new Job
                {
                    JobId = jobId,
                    Source = JobSources.Proof360,
                    CustomerName = customerName,
                    AddressStreet = "100 Mock Street",
                    AddressCity = "Calgary",
                    ServiceType = "plumbing",
                    NotesScope = "outbound test",
                    Status = JobStatuses.Qualified,
                    AssignedVendorId = vendorId
                });
            await store.AddIdentityLinkAsync(
                new ProviderIdentityLink
                {
                    Id = Guid.NewGuid(),
                    ProviderName = ProviderNames.FieldFlow,
                    ProviderInstanceId = OutboundDispatchTestsAccessor.Instance,
                    ExternalEntityType = ExternalEntityTypes.Contractor,
                    ExternalId = "ctr-1001",
                    CanonicalEntityType = CanonicalEntityTypes.Vendor,
                    CanonicalId = vendorId
                });
            await sp.GetRequiredService<IConnectorUnitOfWork>().SaveChangesAsync();
            return jobId;
        });
    }

    public async Task ScopedAsync(Func<IServiceProvider, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider);
    }

    public async Task<T> ScopedAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

file static class OutboundDispatchTestsAccessor
{
    public const string Instance = "fieldflow-test-1";
}

internal sealed class FakeCaps : IProviderCapabilities
{
    public string ProviderName => ProviderNames.FieldFlow;
    public string ProviderInstanceId => OutboundDispatchTestsAccessor.Instance;
    public ProviderCapability SupportedCapabilities =>
        ProviderCapability.WorkOrderDispatch | ProviderCapability.WorkOrderReconcile;
    public bool Supports(ProviderCapability capability) => SupportedCapabilities.HasFlag(capability);
}

internal sealed class RecordingDispatcher : IWorkOrderDispatcher
{
    private readonly string? _succeedWithId;
    private readonly bool _ambiguous;
    private readonly bool _failPermanent;
    private int _failTransientRemaining;
    private readonly string? _customerNameEcho;
    private readonly ConcurrentQueue<string> _keys = new();

    public RecordingDispatcher(
        string? succeedWithId = "wo-default",
        bool ambiguous = false,
        bool failPermanent = false,
        int failTransientTimes = 0,
        string? customerNameEcho = null)
    {
        _succeedWithId = succeedWithId;
        _ambiguous = ambiguous;
        _failPermanent = failPermanent;
        _failTransientRemaining = failTransientTimes;
        _customerNameEcho = customerNameEcho;
    }

    public int DispatchCount { get; private set; }
    public IReadOnlyCollection<string> Keys => _keys.ToArray();

    public Task<Result<WorkOrderSnapshot, ProviderFailure>> DispatchAsync(
        DispatchWorkOrderCommand command,
        CancellationToken cancellationToken)
    {
        DispatchCount++;
        _keys.Enqueue(command.IdempotencyKey);

        if (_failPermanent)
        {
            return Task.FromResult(Result<WorkOrderSnapshot, ProviderFailure>.Fail(
                new ProviderFailure(ProviderFailureKind.Validation, "invalid_request", "permanent")));
        }

        if (_failTransientRemaining > 0)
        {
            _failTransientRemaining--;
            return Task.FromResult(Result<WorkOrderSnapshot, ProviderFailure>.Fail(
                new ProviderFailure(ProviderFailureKind.Unavailable, "unavailable", "transient")));
        }

        if (_ambiguous)
        {
            return Task.FromResult(Result<WorkOrderSnapshot, ProviderFailure>.Fail(
                new ProviderFailure(ProviderFailureKind.AmbiguousWrite, "ambiguous_write", "lost response")));
        }

        return Task.FromResult(Result<WorkOrderSnapshot, ProviderFailure>.Ok(new WorkOrderSnapshot
        {
            ProviderName = ProviderNames.FieldFlow,
            ProviderInstanceId = OutboundDispatchTestsAccessor.Instance,
            ExternalWorkOrderId = _succeedWithId!,
            ClientReference = command.ClientReference,
            ProviderStatus = "open",
            EntityVersion = 1,
            CustomerName = _customerNameEcho ?? command.CustomerName
        }));
    }
}

internal sealed class RecordingReconciler : IWorkOrderReconciler
{
    private Result<WorkOrderSnapshot, ProviderFailure> _byClientRef;
    private string? _expectedClientReference;

    public RecordingReconciler(Result<WorkOrderSnapshot, ProviderFailure> byClientRef) =>
        _byClientRef = byClientRef;

    public int LookupCount { get; private set; }

    public void SetClientReference(string clientReference)
    {
        _expectedClientReference = clientReference;
        if (_byClientRef.IsSuccess)
        {
            var value = ((Result<WorkOrderSnapshot, ProviderFailure>.Succeeded)_byClientRef).Value;
            _byClientRef = Result<WorkOrderSnapshot, ProviderFailure>.Ok(value with { ClientReference = clientReference });
        }
    }

    public Task<Result<WorkOrderSnapshot, ProviderFailure>> GetByExternalIdAsync(
        string externalWorkOrderId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result<WorkOrderSnapshot, ProviderFailure>.Fail(
            new ProviderFailure(ProviderFailureKind.NotFound, "not_found", "missing")));

    public Task<Result<WorkOrderSnapshot, ProviderFailure>> GetByClientReferenceAsync(
        string clientReference,
        CancellationToken cancellationToken)
    {
        LookupCount++;
        if (_expectedClientReference is not null &&
            !string.Equals(_expectedClientReference, clientReference, StringComparison.Ordinal))
        {
            return Task.FromResult(Result<WorkOrderSnapshot, ProviderFailure>.Fail(
                new ProviderFailure(ProviderFailureKind.NotFound, "not_found", "mismatch")));
        }

        return Task.FromResult(_byClientRef);
    }
}

internal sealed class EmptyContractors : IContractorSnapshotSource
{
    public Task<Result<IReadOnlyList<ContractorSnapshot>, ProviderFailure>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Result<IReadOnlyList<ContractorSnapshot>, ProviderFailure>.Ok(Array.Empty<ContractorSnapshot>()));

    public Task<Result<ContractorSnapshot, ProviderFailure>> GetAsync(string externalContractorId, CancellationToken cancellationToken) =>
        Task.FromResult(Result<ContractorSnapshot, ProviderFailure>.Fail(
            new ProviderFailure(ProviderFailureKind.NotFound, "not_found", "missing")));
}

internal sealed class EmptyWorkOrders : IWorkOrderSnapshotSource
{
    public Task<Result<IReadOnlyList<WorkOrderSnapshot>, ProviderFailure>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Result<IReadOnlyList<WorkOrderSnapshot>, ProviderFailure>.Ok(Array.Empty<WorkOrderSnapshot>()));

    public Task<Result<WorkOrderSnapshot, ProviderFailure>> GetAsync(string externalWorkOrderId, CancellationToken cancellationToken) =>
        Task.FromResult(Result<WorkOrderSnapshot, ProviderFailure>.Fail(
            new ProviderFailure(ProviderFailureKind.NotFound, "not_found", "missing")));
}

internal sealed class NoopVerifier : IWebhookVerifier
{
    public WebhookVerificationResult Verify(WebhookVerificationRequest request) =>
        WebhookVerificationResult.Valid();
}

internal sealed class NoopNormalizer : IInboundWebhookNormalizer
{
    public Task<Result<NormalizedWebhookEvent, ProviderFailure>> NormalizeAsync(
        WebhookNormalizeRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result<NormalizedWebhookEvent, ProviderFailure>.Fail(
            ProviderFailure.Unsupported("not used")));
}
