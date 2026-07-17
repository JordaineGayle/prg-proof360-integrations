using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PRG.Proof360.Integrations.Application.Abstractions.Persistence;
using PRG.Proof360.Integrations.Application.Contractors;
using PRG.Proof360.Integrations.Application.DependencyInjection;
using PRG.Proof360.Integrations.Core.Providers.Capabilities;
using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Core.Results;
using PRG.Proof360.Integrations.FieldFlow;
using PRG.Proof360.Integrations.FieldFlow.DependencyInjection;
using PRG.Proof360.Integrations.Infrastructure.Persistence;
using PRG.Proof360.Integrations.IntegrationTests.Inbound;

namespace PRG.Proof360.Integrations.IntegrationTests.Webhooks;

internal static class WebhookTestSecrets
{
    public const string Secret = "webhook-test-secret";
    public const string Instance = InboundSyncTestFixture.ProviderInstanceId;
}

internal sealed class WebhookFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private WebhookFixture(SqliteConnection connection, ServiceProvider services)
    {
        _connection = connection;
        Services = services;
    }

    public ServiceProvider Services { get; }

    public static Task<WebhookFixture> CreateAsync(bool seedContractor = false) =>
        CreateCoreAsync("Data Source=:memory:", seedContractor);

    public static Task<WebhookFixture> CreateFileAsync(string path, bool seedContractor = false) =>
        CreateCoreAsync($"Data Source={path}", seedContractor);

    private static async Task<WebhookFixture> CreateCoreAsync(string connectionString, bool seedContractor)
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.Configure<InboundSyncOptions>(o => o.MaxProcessBatch = 50);
        services.Configure<FieldFlowOptions>(o =>
        {
            o.BaseUrl = "http://localhost:5210";
            o.WebhookHmacSecret = WebhookTestSecrets.Secret;
            o.ProviderInstanceId = WebhookTestSecrets.Instance;
            o.WebhookTimestampSkewSeconds = 300;
        });
        services.AddFieldFlow();
        services.AddDbContext<ConnectorDbContext>(o => o.UseSqlite(connection));
        services.AddScoped<IConnectorUnitOfWork, ConnectorUnitOfWork>();
        services.AddScoped<ICanonicalWriter, CanonicalWriter>();
        services.AddScoped<IIntegrationStore, IntegrationStore>();
        services.AddSingleton<IPersistenceExceptionClassifier, PersistenceExceptionClassifier>();

        services.RemoveAll<IContractorSnapshotSource>();
        services.RemoveAll<IWorkOrderSnapshotSource>();
        var contractors = seedContractor ? new[] { InboundFixtures.Contractor() } : Array.Empty<ContractorSnapshot>();
        services.AddSingleton<IContractorSnapshotSource>(new FixedContractorSource(contractors));
        services.AddSingleton<IWorkOrderSnapshotSource>(new EmptyWorkOrderSource());

        var provider = services.BuildServiceProvider();
        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ConnectorDbContext>().Database.EnsureCreatedAsync();
            if (seedContractor)
            {
                Assert.True((await scope.ServiceProvider.GetRequiredService<ImportContractorsHandler>()
                    .HandleAsync(CancellationToken.None)).IsSuccess);
            }
        }

        return new WebhookFixture(connection, provider);
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
}

internal sealed class FixedContractorSource(IReadOnlyList<ContractorSnapshot> items) : IContractorSnapshotSource
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

internal sealed class EmptyWorkOrderSource : IWorkOrderSnapshotSource
{
    public Task<Result<IReadOnlyList<WorkOrderSnapshot>, ProviderFailure>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Result<IReadOnlyList<WorkOrderSnapshot>, ProviderFailure>.Ok(Array.Empty<WorkOrderSnapshot>()));

    public Task<Result<WorkOrderSnapshot, ProviderFailure>> GetAsync(string externalWorkOrderId, CancellationToken cancellationToken) =>
        Task.FromResult(Result<WorkOrderSnapshot, ProviderFailure>.Fail(
            new ProviderFailure(ProviderFailureKind.NotFound, "not_found", "missing")));
}
