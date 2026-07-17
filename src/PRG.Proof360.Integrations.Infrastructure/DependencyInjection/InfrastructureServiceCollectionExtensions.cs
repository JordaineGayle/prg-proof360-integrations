using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PRG.Proof360.Integrations.Application.Abstractions.Persistence;
using PRG.Proof360.Integrations.Infrastructure.Persistence;

namespace PRG.Proof360.Integrations.Infrastructure.DependencyInjection;

/// <summary>
/// Composition helpers for persistence, workers, and technical adapters.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers infrastructure services for the connector composition root.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<ConnectorPersistenceOptions>(configuration.GetSection(ConnectorPersistenceOptions.SectionName));

        var connectionString = configuration.GetSection(ConnectorPersistenceOptions.SectionName)["ConnectionString"]
            ?? "Data Source=connector.db";

        services.AddDbContext<ConnectorDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IConnectorUnitOfWork, ConnectorUnitOfWork>();
        services.AddScoped<ICanonicalWriter, CanonicalWriter>();
        services.AddScoped<IIntegrationStore, IntegrationStore>();
        services.AddHostedService<ConnectorDatabaseInitializer>();

        return services;
    }
}
