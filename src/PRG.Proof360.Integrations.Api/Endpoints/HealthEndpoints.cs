using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using PRG.Proof360.Integrations.Application.Health;

namespace PRG.Proof360.Integrations.Api.Endpoints;

/// <summary>
/// Maps process liveness, readiness, and connector-specific health endpoints.
/// </summary>
public static class HealthEndpoints
{
    /// <summary>
    /// Maps <c>/health/live</c>, <c>/health/ready</c>, and <c>/connectors/fieldflow/health</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapConnectorHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // Liveness: process is alive. Provider outage must not fail this check.
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = static _ => false
        });

        // Readiness: local dependencies (e.g. SQLite) are initialized.
        // FieldFlow outage does not fail readiness — work can still be queued durably.
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = static check => check.Tags.Contains("ready")
        });

        endpoints.MapGet("/connectors/fieldflow/health", async (
                GetConnectorHealthHandler handler,
                CancellationToken cancellationToken) =>
            {
                var snapshot = await handler.HandleAsync(cancellationToken);
                return Results.Json(snapshot);
            })
            .WithName("GetFieldFlowConnectorHealth");

        return endpoints;
    }
}
