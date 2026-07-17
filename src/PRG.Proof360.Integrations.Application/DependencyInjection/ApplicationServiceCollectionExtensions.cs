using Microsoft.Extensions.DependencyInjection;

namespace PRG.Proof360.Integrations.Application.DependencyInjection;

/// <summary>
/// Composition helpers for application use cases. Implementations are registered in later prompts.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers application-layer services for the connector composition root.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
