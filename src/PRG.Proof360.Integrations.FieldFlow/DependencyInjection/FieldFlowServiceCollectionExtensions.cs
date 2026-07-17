using Microsoft.Extensions.DependencyInjection;

namespace PRG.Proof360.Integrations.FieldFlow.DependencyInjection;

/// <summary>
/// Composition helpers for the FieldFlow adapter. HTTP clients and mappers are registered in later prompts.
/// </summary>
public static class FieldFlowServiceCollectionExtensions
{
    /// <summary>
    /// Registers FieldFlow adapter services for the connector composition root.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddFieldFlow(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
