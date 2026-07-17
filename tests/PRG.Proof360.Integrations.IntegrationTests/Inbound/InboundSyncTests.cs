using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRG.Proof360.Integrations.Application.Abstractions.Persistence;
using PRG.Proof360.Integrations.Application.Contractors;
using PRG.Proof360.Integrations.Application.DependencyInjection;
using PRG.Proof360.Integrations.Application.Inbox;
using PRG.Proof360.Integrations.Application.Mapping;
using PRG.Proof360.Integrations.Application.Outcomes;
using PRG.Proof360.Integrations.Application.WorkOrders;
using PRG.Proof360.Integrations.Core.Integration;
using PRG.Proof360.Integrations.Core.Providers;
using PRG.Proof360.Integrations.Core.Providers.Capabilities;
using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Core.Results;
using PRG.Proof360.Integrations.Domain.Canonical;
using PRG.Proof360.Integrations.Infrastructure.Persistence;

namespace PRG.Proof360.Integrations.IntegrationTests.Inbound;

public sealed class InboundSyncTests
{
    [Fact]
    public async Task Repeating_contractor_snapshot_yields_one_vendor_and_link()
    {
        await using var fx = await InboundSyncTestFixture.CreateAsync([InboundFixtures.Contractor()]);
        await fx.ScopedAsync(async sp =>
        {
            var import = sp.GetRequiredService<ImportContractorsHandler>();
            Assert.True((await import.HandleAsync(CancellationToken.None)).IsSuccess);
            Assert.True((await import.HandleAsync(CancellationToken.None)).IsSuccess);

            var canonical = sp.GetRequiredService<ICanonicalWriter>();
            var store = sp.GetRequiredService<IIntegrationStore>();
            Assert.Equal(1, await canonical.CountVendorsAsync());
            Assert.Equal(1, await store.CountIdentityLinksAsync(InboundSyncTestFixture.ProviderInstanceId, ExternalEntityTypes.Contractor));
        });
    }

    [Fact]
    public async Task Repeating_work_order_snapshot_yields_one_job_and_link()
    {
        await using var fx = await InboundSyncTestFixture.CreateAsync(
            [InboundFixtures.Contractor()],
            [InboundFixtures.WorkOrder()]);

        await fx.ScopedAsync(async sp =>
        {
            Assert.True((await sp.GetRequiredService<ImportContractorsHandler>().HandleAsync(CancellationToken.None)).IsSuccess);
            var import = sp.GetRequiredService<ImportWorkOrdersHandler>();
            Assert.True((await import.HandleAsync(CancellationToken.None)).IsSuccess);
            Assert.True((await import.HandleAsync(CancellationToken.None)).IsSuccess);

            Assert.Equal(1, await sp.GetRequiredService<ICanonicalWriter>().CountJobsAsync());
            Assert.Equal(
                1,
                await sp.GetRequiredService<IIntegrationStore>()
                    .CountIdentityLinksAsync(InboundSyncTestFixture.ProviderInstanceId, ExternalEntityTypes.WorkOrder));
        });
    }

    [Fact]
    public async Task Poll_and_equivalent_synthetic_event_converge_to_one_effect()
    {
        var contractor = InboundFixtures.Contractor();
        await using var fx = await InboundSyncTestFixture.CreateAsync([contractor]);

        await fx.ScopedAsync(async sp =>
        {
            Assert.True((await sp.GetRequiredService<ImportContractorsHandler>().HandleAsync(CancellationToken.None)).IsSuccess);

            var receive = sp.GetRequiredService<ReceiveProviderEventHandler>();
            var envelope = JsonSerializer.Serialize(contractor);
            var again = await receive.HandleAsync(
                new ReceiveProviderEventCommand
                {
                    ProviderName = contractor.ProviderName,
                    ProviderInstanceId = contractor.ProviderInstanceId,
                    EventId = SyntheticEventIds.ForContractor(contractor),
                    EventType = InboxEventTypes.ContractorSnapshot,
                    PayloadEnvelope = envelope,
                    PayloadHash = SyntheticEventIds.HashPayload(contractor),
                    EventVersion = contractor.EntityVersion,
                    OccurredAt = DateTimeOffset.UtcNow
                },
                CancellationToken.None);

            Assert.True(again.IsSuccess);
            Assert.IsType<ReceiveEventOutcome.Duplicate>(
                ((Result<ReceiveEventOutcome, Application.Errors.IntegrationFailure>.Succeeded)again).Value);
            Assert.Equal(1, await sp.GetRequiredService<ICanonicalWriter>().CountVendorsAsync());
        });
    }

    [Fact]
    public async Task Concurrent_duplicate_snapshots_create_one_canonical_row()
    {
        var path = Path.Combine(Path.GetTempPath(), $"prg-inbound-{Guid.NewGuid():N}.db");
        var contractor = InboundFixtures.Contractor();
        try
        {
            await using (var setup = CreateFileContext(path))
            {
                await setup.Database.EnsureCreatedAsync();
            }

            var tasks = Enumerable.Range(0, 6).Select(_ => ImportOnceAgainstFileAsync(path, contractor));
            await Task.WhenAll(tasks);

            await using var verify = CreateFileContext(path);
            Assert.Equal(1, await verify.Vendors.CountAsync());
            Assert.Equal(1, await verify.ProviderIdentityLinks.CountAsync(x => x.ExternalEntityType == ExternalEntityTypes.Contractor));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Approved_vendor_is_not_auto_demoted_by_active_provider()
    {
        await using var fx = await InboundSyncTestFixture.CreateAsync([InboundFixtures.Contractor()]);
        await fx.ScopedAsync(async sp =>
        {
            Assert.True((await sp.GetRequiredService<ImportContractorsHandler>().HandleAsync(CancellationToken.None)).IsSuccess);
            var db = sp.GetRequiredService<ConnectorDbContext>();
            var vendor = await db.Vendors.SingleAsync();
            vendor.Status = VendorStatuses.Approved;
            await db.SaveChangesAsync();
        });

        await fx.ScopedAsync(async sp =>
        {
            var updated = InboundFixtures.Contractor(version: 2);
            var envelope = JsonSerializer.Serialize(updated);
            Assert.True((await sp.GetRequiredService<ReceiveProviderEventHandler>().HandleAsync(
                new ReceiveProviderEventCommand
                {
                    ProviderName = updated.ProviderName,
                    ProviderInstanceId = updated.ProviderInstanceId,
                    EventId = SyntheticEventIds.ForContractor(updated),
                    EventType = InboxEventTypes.ContractorSnapshot,
                    PayloadEnvelope = envelope,
                    PayloadHash = SyntheticEventIds.HashPayload(updated),
                    EventVersion = 2,
                    OccurredAt = DateTimeOffset.UtcNow
                },
                CancellationToken.None)).IsSuccess);

            Assert.True((await sp.GetRequiredService<ProcessInboxMessageHandler>()
                .HandleAsync(InboundSyncTestFixture.ProviderInstanceId, CancellationToken.None)).IsSuccess);

            var vendor = await sp.GetRequiredService<ConnectorDbContext>().Vendors.SingleAsync();
            Assert.Equal(VendorStatuses.Approved, vendor.Status);
        });
    }

    [Fact]
    public async Task Proof360_originated_job_fields_are_not_overwritten()
    {
        await using var fx = await InboundSyncTestFixture.CreateAsync(
            [InboundFixtures.Contractor()],
            [InboundFixtures.WorkOrder()]);

        Guid jobId = default;
        await fx.ScopedAsync(async sp =>
        {
            Assert.True((await sp.GetRequiredService<ImportContractorsHandler>().HandleAsync(CancellationToken.None)).IsSuccess);
            Assert.True((await sp.GetRequiredService<ImportWorkOrdersHandler>().HandleAsync(CancellationToken.None)).IsSuccess);

            var db = sp.GetRequiredService<ConnectorDbContext>();
            var job = await db.Jobs.SingleAsync();
            job.Source = JobSources.Proof360;
            job.CustomerName = "Proof360 Customer";
            job.AddressCity = "Toronto";
            await db.SaveChangesAsync();
            jobId = job.JobId;
        });

        await fx.ScopedAsync(async sp =>
        {
            var echo = InboundFixtures.WorkOrder(status: "scheduled", version: 2) with
            {
                CustomerName = "Provider Echo",
                AddressCity = "Calgary"
            };
            var envelope = JsonSerializer.Serialize(echo);
            Assert.True((await sp.GetRequiredService<ReceiveProviderEventHandler>().HandleAsync(
                new ReceiveProviderEventCommand
                {
                    ProviderName = echo.ProviderName,
                    ProviderInstanceId = echo.ProviderInstanceId,
                    EventId = SyntheticEventIds.ForWorkOrder(echo),
                    EventType = InboxEventTypes.WorkOrderSnapshot,
                    PayloadEnvelope = envelope,
                    PayloadHash = SyntheticEventIds.HashPayload(echo),
                    EventVersion = 2,
                    OccurredAt = DateTimeOffset.UtcNow
                },
                CancellationToken.None)).IsSuccess);

            Assert.True((await sp.GetRequiredService<ProcessInboxMessageHandler>()
                .HandleAsync(InboundSyncTestFixture.ProviderInstanceId, CancellationToken.None)).IsSuccess);

            var job = await sp.GetRequiredService<ICanonicalWriter>().FindJobAsync(jobId);
            Assert.Equal("Proof360 Customer", job!.CustomerName);
            Assert.Equal("Toronto", job.AddressCity);
            Assert.Equal(JobStatuses.Scheduled, job.Status);
        });
    }

    [Fact]
    public async Task Unknown_contractor_waits_for_dependency_without_partial_job()
    {
        await using var fx = await InboundSyncTestFixture.CreateAsync(
            contractors: [],
            workOrders: [InboundFixtures.WorkOrder(contractorId: "ctr-missing")]);

        await fx.ScopedAsync(async sp =>
        {
            Assert.True((await sp.GetRequiredService<ImportWorkOrdersHandler>().HandleAsync(CancellationToken.None)).IsSuccess);
            Assert.Equal(0, await sp.GetRequiredService<ICanonicalWriter>().CountJobsAsync());

            var inbox = await sp.GetRequiredService<ConnectorDbContext>().InboxMessages.SingleAsync();
            Assert.Equal(InboxMessageStates.WaitingForDependency, inbox.State);
        });
    }

    [Fact]
    public async Task Importing_contractor_then_reprocessing_work_order_succeeds_once()
    {
        var workOrder = InboundFixtures.WorkOrder(contractorId: "ctr-1001");
        await using var fx = await InboundSyncTestFixture.CreateAsync(
            [InboundFixtures.Contractor()],
            [workOrder]);

        await fx.ScopedAsync(async sp =>
        {
            var envelope = JsonSerializer.Serialize(workOrder);
            Assert.True((await sp.GetRequiredService<ReceiveProviderEventHandler>().HandleAsync(
                new ReceiveProviderEventCommand
                {
                    ProviderName = workOrder.ProviderName,
                    ProviderInstanceId = workOrder.ProviderInstanceId,
                    EventId = SyntheticEventIds.ForWorkOrder(workOrder),
                    EventType = InboxEventTypes.WorkOrderSnapshot,
                    PayloadEnvelope = envelope,
                    PayloadHash = SyntheticEventIds.HashPayload(workOrder),
                    EventVersion = workOrder.EntityVersion,
                    OccurredAt = DateTimeOffset.UtcNow
                },
                CancellationToken.None)).IsSuccess);

            var process = sp.GetRequiredService<ProcessInboxMessageHandler>();
            var waiting = await process.HandleAsync(InboundSyncTestFixture.ProviderInstanceId, CancellationToken.None);
            Assert.True(waiting.IsSuccess);
            Assert.IsType<ProcessInboxOutcome.WaitingForDependency>(
                ((Result<ProcessInboxOutcome, Application.Errors.IntegrationFailure>.Succeeded)waiting).Value);

            Assert.True((await sp.GetRequiredService<ImportContractorsHandler>().HandleAsync(CancellationToken.None)).IsSuccess);

            var db = sp.GetRequiredService<ConnectorDbContext>();
            var msg = await db.InboxMessages.SingleAsync(x => x.EventType == InboxEventTypes.WorkOrderSnapshot);
            msg.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            msg.State = InboxMessageStates.WaitingForDependency;
            await db.SaveChangesAsync();

            var applied = await process.HandleAsync(InboundSyncTestFixture.ProviderInstanceId, CancellationToken.None);
            Assert.True(applied.IsSuccess);
            Assert.Equal(1, await sp.GetRequiredService<ICanonicalWriter>().CountJobsAsync());

            Assert.True((await sp.GetRequiredService<ImportWorkOrdersHandler>().HandleAsync(CancellationToken.None)).IsSuccess);
            Assert.Equal(1, await sp.GetRequiredService<ICanonicalWriter>().CountJobsAsync());
        });
    }

    [Fact]
    public async Task Worker_lease_prevents_double_processing()
    {
        await using var fx = await InboundSyncTestFixture.CreateAsync([InboundFixtures.Contractor()]);
        await fx.ScopedAsync(async sp =>
        {
            var contractor = InboundFixtures.Contractor();
            var envelope = JsonSerializer.Serialize(contractor);
            Assert.True((await sp.GetRequiredService<ReceiveProviderEventHandler>().HandleAsync(
                new ReceiveProviderEventCommand
                {
                    ProviderName = contractor.ProviderName,
                    ProviderInstanceId = contractor.ProviderInstanceId,
                    EventId = SyntheticEventIds.ForContractor(contractor),
                    EventType = InboxEventTypes.ContractorSnapshot,
                    PayloadEnvelope = envelope,
                    PayloadHash = SyntheticEventIds.HashPayload(contractor),
                    EventVersion = contractor.EntityVersion,
                    OccurredAt = DateTimeOffset.UtcNow
                },
                CancellationToken.None)).IsSuccess);

            var process = sp.GetRequiredService<ProcessInboxMessageHandler>();
            var first = await process.HandleAsync(InboundSyncTestFixture.ProviderInstanceId, CancellationToken.None);
            var second = await process.HandleAsync(InboundSyncTestFixture.ProviderInstanceId, CancellationToken.None);

            Assert.True(first.IsSuccess);
            Assert.IsType<ProcessInboxOutcome.ContractorApplied>(
                ((Result<ProcessInboxOutcome, Application.Errors.IntegrationFailure>.Succeeded)first).Value);
            Assert.IsType<ProcessInboxOutcome.Idle>(
                ((Result<ProcessInboxOutcome, Application.Errors.IntegrationFailure>.Succeeded)second).Value);
            Assert.Equal(1, await sp.GetRequiredService<ICanonicalWriter>().CountVendorsAsync());
        });
    }

    [Fact]
    public async Task Cancellation_stops_import_safely()
    {
        await using var fx = await InboundSyncTestFixture.CreateAsync([InboundFixtures.Contractor()]);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await fx.ScopedAsync(async sp =>
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                sp.GetRequiredService<ImportContractorsHandler>().HandleAsync(cts.Token));
        });
    }

    [Fact]
    public async Task Repeated_no_change_snapshots_audit_without_row_duplication()
    {
        var contractor = InboundFixtures.Contractor(version: 1);
        await using var fx = await InboundSyncTestFixture.CreateAsync([contractor]);
        await fx.ScopedAsync(async sp =>
        {
            Assert.True((await sp.GetRequiredService<ImportContractorsHandler>().HandleAsync(CancellationToken.None)).IsSuccess);

            // Distinct event id, same entity version → apply path records no_change (poll duplicate would short-circuit at receive).
            var envelope = JsonSerializer.Serialize(contractor);
            Assert.True((await sp.GetRequiredService<ReceiveProviderEventHandler>().HandleAsync(
                new ReceiveProviderEventCommand
                {
                    ProviderName = contractor.ProviderName,
                    ProviderInstanceId = contractor.ProviderInstanceId,
                    EventId = "webhook:contractor:ctr-1001:v1",
                    EventType = InboxEventTypes.ContractorSnapshot,
                    PayloadEnvelope = envelope,
                    PayloadHash = SyntheticEventIds.HashPayload(contractor),
                    EventVersion = 1,
                    OccurredAt = DateTimeOffset.UtcNow
                },
                CancellationToken.None)).IsSuccess);

            Assert.True((await sp.GetRequiredService<ProcessInboxMessageHandler>()
                .HandleAsync(InboundSyncTestFixture.ProviderInstanceId, CancellationToken.None)).IsSuccess);

            Assert.Equal(1, await sp.GetRequiredService<ICanonicalWriter>().CountVendorsAsync());
            Assert.True(
                await sp.GetRequiredService<IIntegrationStore>()
                    .CountAuditEventsAsync("contractor.apply", "no_change") >= 1);
        });
    }

    [Fact]
    public async Task Failed_apply_does_not_mark_inbox_completed_or_create_canonical_rows()
    {
        await using var fx = await InboundSyncTestFixture.CreateAsync();
        await fx.ScopedAsync(async sp =>
        {
            Assert.True((await sp.GetRequiredService<ReceiveProviderEventHandler>().HandleAsync(
                new ReceiveProviderEventCommand
                {
                    ProviderName = "FieldFlow",
                    ProviderInstanceId = InboundSyncTestFixture.ProviderInstanceId,
                    EventId = "poll:bad",
                    EventType = InboxEventTypes.ContractorSnapshot,
                    PayloadEnvelope = "{not-json",
                    PayloadHash = "x",
                    OccurredAt = DateTimeOffset.UtcNow
                },
                CancellationToken.None)).IsSuccess);

            var outcome = await sp.GetRequiredService<ProcessInboxMessageHandler>()
                .HandleAsync(InboundSyncTestFixture.ProviderInstanceId, CancellationToken.None);
            Assert.True(outcome.IsSuccess);
            Assert.IsType<ProcessInboxOutcome.DeadLettered>(
                ((Result<ProcessInboxOutcome, Application.Errors.IntegrationFailure>.Succeeded)outcome).Value);

            var inbox = await sp.GetRequiredService<ConnectorDbContext>().InboxMessages.SingleAsync();
            Assert.Equal(InboxMessageStates.DeadLettered, inbox.State);
            Assert.Equal(0, await sp.GetRequiredService<ICanonicalWriter>().CountVendorsAsync());
        });
    }

    private static ConnectorDbContext CreateFileContext(string path) =>
        new(new DbContextOptionsBuilder<ConnectorDbContext>().UseSqlite($"Data Source={path}").Options);

    private static async Task ImportOnceAgainstFileAsync(string path, ContractorSnapshot contractor)
    {
        var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync();
        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddApplication();
            services.Configure<InboundSyncOptions>(o => o.MaxProcessBatch = 50);
            services.AddDbContext<ConnectorDbContext>(o => o.UseSqlite(connection));
            services.AddScoped<IConnectorUnitOfWork, ConnectorUnitOfWork>();
            services.AddScoped<ICanonicalWriter, CanonicalWriter>();
            services.AddScoped<IIntegrationStore, IntegrationStore>();
            services.AddSingleton<IPersistenceExceptionClassifier, PersistenceExceptionClassifier>();
            services.AddSingleton<IProviderCapabilities>(new FileTestCapabilities());
            services.AddSingleton<IContractorSnapshotSource>(new FixedContractorSource(contractor));
            services.AddSingleton<IWorkOrderSnapshotSource>(new EmptyWorkOrderSource());

            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ImportContractorsHandler>()
                .HandleAsync(CancellationToken.None);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    private sealed class FileTestCapabilities : IProviderCapabilities
    {
        public string ProviderName => ProviderNames.FieldFlow;
        public string ProviderInstanceId => InboundSyncTestFixture.ProviderInstanceId;
        public ProviderCapability SupportedCapabilities => ProviderCapability.ContractorSnapshots;
        public bool Supports(ProviderCapability capability) => SupportedCapabilities.HasFlag(capability);
    }

    private sealed class FixedContractorSource(ContractorSnapshot item) : IContractorSnapshotSource
    {
        public Task<Result<IReadOnlyList<ContractorSnapshot>, ProviderFailure>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Result<IReadOnlyList<ContractorSnapshot>, ProviderFailure>.Ok(new[] { item }));

        public Task<Result<ContractorSnapshot, ProviderFailure>> GetAsync(string externalContractorId, CancellationToken cancellationToken) =>
            Task.FromResult(Result<ContractorSnapshot, ProviderFailure>.Ok(item));
    }

    private sealed class EmptyWorkOrderSource : IWorkOrderSnapshotSource
    {
        public Task<Result<IReadOnlyList<WorkOrderSnapshot>, ProviderFailure>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Result<IReadOnlyList<WorkOrderSnapshot>, ProviderFailure>.Ok(Array.Empty<WorkOrderSnapshot>()));

        public Task<Result<WorkOrderSnapshot, ProviderFailure>> GetAsync(string externalWorkOrderId, CancellationToken cancellationToken) =>
            Task.FromResult(Result<WorkOrderSnapshot, ProviderFailure>.Fail(
                new ProviderFailure(ProviderFailureKind.NotFound, "not_found", "missing")));
    }
}
