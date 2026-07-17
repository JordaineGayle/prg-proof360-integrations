using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PRG.Proof360.Integrations.Application.Abstractions.Persistence;
using PRG.Proof360.Integrations.Application.Contractors;
using PRG.Proof360.Integrations.Application.Dispatch;
using PRG.Proof360.Integrations.Infrastructure.Health;
using PRG.Proof360.Integrations.Infrastructure.Persistence;
using PRG.Proof360.Integrations.Infrastructure.Workers;

namespace PRG.Proof360.Integrations.Infrastructure.DependencyInjection;

/// <summary>
/// Composition helpers for persistence, workers, and technical adapters.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers infrastructure services for the connector composition root.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<ConnectorPersistenceOptions>(configuration.GetSection(ConnectorPersistenceOptions.SectionName));
        services.Configure<InboundSyncOptions>(configuration.GetSection(InboundSyncOptions.SectionName));
        services.Configure<OutboundDispatchOptions>(configuration.GetSection(OutboundDispatchOptions.SectionName));

        var connectionString = configuration.GetSection(ConnectorPersistenceOptions.SectionName)["ConnectionString"]
            ?? "Data Source=connector.db";

        services.AddDbContext<ConnectorDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IConnectorUnitOfWork, ConnectorUnitOfWork>();
        services.AddScoped<ICanonicalWriter, CanonicalWriter>();
        services.AddScoped<IIntegrationStore, IntegrationStore>();
        services.AddSingleton<IPersistenceExceptionClassifier, PersistenceExceptionClassifier>();
        services.AddHealthChecks()
            .AddCheck<SqliteReadyHealthCheck>(
                "sqlite",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"]);
        services.AddHostedService<ConnectorDatabaseInitializer>();
        services.AddHostedService<InboundPollingWorker>();
        services.AddHostedService<OutboundDispatchWorker>();

        return services;
    }
}
