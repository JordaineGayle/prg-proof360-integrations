using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace PRG.Proof360.Integrations.Api.Endpoints;

/// <summary>
/// Maps process liveness and readiness health endpoints for the connector API.
/// </summary>
public static class HealthEndpoints
{
    /// <summary>
    /// Maps <c>/health/live</c> and <c>/health/ready</c> placeholder checks.
    /// Connector-specific status is added in a later prompt.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same builder for chaining.</returns>
    public static IEndpointRouteBuilder MapConnectorHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = static _ => false
        });

        endpoints.MapHealthChecks("/health/ready");

        return endpoints;
    }
}
