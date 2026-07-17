using PRG.Proof360.Integrations.Api.Errors;
using PRG.Proof360.Integrations.Application.Demo;
using PRG.Proof360.Integrations.Application.Dispatch;

namespace PRG.Proof360.Integrations.Api.Endpoints;

/// <summary>
/// Local/demo-only endpoints. Registered only in Development.
/// </summary>
public static class DemoSeedEndpoints
{
    /// <summary>Maps demo seed routes.</summary>
    public static IEndpointRouteBuilder MapDemoSeedEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/_demo/seed-qualified-dispatch", async (
            SeedQualifiedDispatchDemoHandler handler,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);
            return result.ToHttpResult(http, outcome => Results.Ok(new
            {
                jobId = outcome.JobId,
                vendorId = outcome.VendorId,
                created = outcome.Created,
                dispatch = $"/connectors/fieldflow/jobs/{outcome.JobId}/dispatch"
            }));
        });

        endpoints.MapGet("/_demo/summary", async (
            GetDemoSummaryHandler handler,
            CancellationToken cancellationToken) =>
        {
            var summary = await handler.HandleAsync(cancellationToken);
            return Results.Ok(summary);
        });

        endpoints.MapPost("/_demo/nudge-waiting-dependencies", async (
            NudgeWaitingDependenciesHandler handler,
            CancellationToken cancellationToken) =>
        {
            var (madeDue, processed) = await handler.HandleAsync(maxBatch: 20, cancellationToken);
            return Results.Ok(new { madeDue, processed });
        });

        return endpoints;
    }
}
