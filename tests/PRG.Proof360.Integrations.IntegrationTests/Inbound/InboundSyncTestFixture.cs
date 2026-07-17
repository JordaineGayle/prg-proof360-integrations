using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRG.Proof360.Integrations.Application.Abstractions.Persistence;
using PRG.Proof360.Integrations.Application.Contractors;
using PRG.Proof360.Integrations.Application.DependencyInjection;
using PRG.Proof360.Integrations.Core.Providers;
using PRG.Proof360.Integrations.Core.Providers.Capabilities;
using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Core.Results;
using PRG.Proof360.Integrations.Infrastructure.Persistence;

namespace PRG.Proof360.Integrations.IntegrationTests.Inbound;

internal sealed class InboundSyncTestFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private InboundSyncTestFixture(SqliteConnection connection, ServiceProvider services)
    {
        _connection = connection;
        Services = services;
    }

    public ServiceProvider Services { get; }

    public const string ProviderInstanceId = "fieldflow-test-1";

    public static async Task<InboundSyncTestFixture> CreateAsync(
        IReadOnlyList<ContractorSnapshot>? contractors = null,
        IReadOnlyList<WorkOrderSnapshot>? workOrders = null)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.Configure<InboundSyncOptions>(o =>
        {
            o.PollingEnabled = false;
            o.MaxProcessBatch = 50;
        });

        services.AddDbContext<ConnectorDbContext>(o => o.UseSqlite(connection));
        services.AddScoped<IConnectorUnitOfWork, ConnectorUnitOfWork>();
        services.AddScoped<ICanonicalWriter, CanonicalWriter>();
        services.AddScoped<IIntegrationStore, IntegrationStore>();
        services.AddSingleton<IPersistenceExceptionClassifier, PersistenceExceptionClassifier>();
        services.AddSingleton<IProviderCapabilities>(new FakeCapabilities());
        services.AddSingleton<IContractorSnapshotSource>(new FakeContractorSource(contractors ?? []));
        services.AddSingleton<IWorkOrderSnapshotSource>(new FakeWorkOrderSource(workOrders ?? []));

        var provider = services.BuildServiceProvider();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ConnectorDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        return new InboundSyncTestFixture(connection, provider);
    }

    public async Task<T> ScopedAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    public async Task ScopedAsync(Func<IServiceProvider, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider);
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private sealed class FakeCapabilities : IProviderCapabilities
    {
        public string ProviderName => ProviderNames.FieldFlow;
        public string ProviderInstanceId => InboundSyncTestFixture.ProviderInstanceId;
        public ProviderCapability SupportedCapabilities =>
            ProviderCapability.ContractorSnapshots | ProviderCapability.WorkOrderSnapshots;
        public bool Supports(ProviderCapability capability) => SupportedCapabilities.HasFlag(capability);
    }

    private sealed class FakeContractorSource(IReadOnlyList<ContractorSnapshot> items) : IContractorSnapshotSource
    {
        public Task<Result<IReadOnlyList<ContractorSnapshot>, ProviderFailure>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Result<IReadOnlyList<ContractorSnapshot>, ProviderFailure>.Ok(items));

        public Task<Result<ContractorSnapshot, ProviderFailure>> GetAsync(string externalContractorId, CancellationToken cancellationToken)
        {
            var match = items.FirstOrDefault(x => x.ExternalContractorId == externalContractorId);
            return Task.FromResult(
                match is null
                    ? Result<ContractorSnapshot, ProviderFailure>.Fail(
                        new ProviderFailure(ProviderFailureKind.NotFound, "not_found", "missing"))
                    : Result<ContractorSnapshot, ProviderFailure>.Ok(match));
        }
    }

    private sealed class FakeWorkOrderSource(IReadOnlyList<WorkOrderSnapshot> items) : IWorkOrderSnapshotSource
    {
        public Task<Result<IReadOnlyList<WorkOrderSnapshot>, ProviderFailure>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Result<IReadOnlyList<WorkOrderSnapshot>, ProviderFailure>.Ok(items));

        public Task<Result<WorkOrderSnapshot, ProviderFailure>> GetAsync(string externalWorkOrderId, CancellationToken cancellationToken)
        {
            var match = items.FirstOrDefault(x => x.ExternalWorkOrderId == externalWorkOrderId);
            return Task.FromResult(
                match is null
                    ? Result<WorkOrderSnapshot, ProviderFailure>.Fail(
                        new ProviderFailure(ProviderFailureKind.NotFound, "not_found", "missing"))
                    : Result<WorkOrderSnapshot, ProviderFailure>.Ok(match));
        }
    }
}

internal static class InboundFixtures
{
    public static ContractorSnapshot Contractor(
        string id = "ctr-1001",
        bool active = true,
        long? version = 1) =>
        new()
        {
            ProviderName = ProviderNames.FieldFlow,
            ProviderInstanceId = InboundSyncTestFixture.ProviderInstanceId,
            ExternalContractorId = id,
            ComplianceId = "CMP-1001",
            IsActive = active,
            LicenseNumber = "LIC-1001",
            LicenseExpiry = new DateOnly(2027, 12, 31),
            InsurancePolicy = "INS-1001",
            InsuranceExpiry = new DateOnly(2027, 6, 30),
            InsuranceCoverage = "2000000 CAD",
            WcbNumber = "WCB-1001",
            EntityVersion = version
        };

    public static WorkOrderSnapshot WorkOrder(
        string id = "wo-2001",
        string? contractorId = "ctr-1001",
        string status = "open",
        long version = 1) =>
        new()
        {
            ProviderName = ProviderNames.FieldFlow,
            ProviderInstanceId = InboundSyncTestFixture.ProviderInstanceId,
            ExternalWorkOrderId = id,
            ExternalContractorId = contractorId,
            ProviderStatus = status,
            EntityVersion = version,
            CustomerName = "Ada Fixture",
            AddressStreet = "100 Mock Street",
            AddressCity = "Calgary",
            ServiceType = "plumbing",
            Notes = "fixture"
        };
}
