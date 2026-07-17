using PRG.Proof360.Integrations.Api.Errors;
using PRG.Proof360.Integrations.Application.Contractors;
using PRG.Proof360.Integrations.Application.WorkOrders;

namespace PRG.Proof360.Integrations.Api.Endpoints;

/// <summary>
/// Manual local sync endpoints. Application owns sync logic; endpoints only dispatch.
/// </summary>
public static class SyncEndpoints
{
    /// <summary>Maps sync routes.</summary>
    public static IEndpointRouteBuilder MapSyncEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/sync/contractors", async (
            ImportContractorsHandler handler,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);
            return result.ToHttpResult(http, outcome => Results.Ok(new
            {
                imported = outcome is Application.Outcomes.ImportContractorsOutcome.Completed c
                    ? c.ImportedCount
                    : 0,
                updated = outcome is Application.Outcomes.ImportContractorsOutcome.Completed u
                    ? u.UpdatedCount
                    : 0
            }));
        });

        endpoints.MapPost("/sync/work-orders", async (
            ImportWorkOrdersHandler handler,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);
            return result.ToHttpResult(http, outcome => Results.Ok(new
            {
                created = outcome is ImportWorkOrdersOutcome.Completed c ? c.CreatedCount : 0,
                updated = outcome is ImportWorkOrdersOutcome.Completed u ? u.UpdatedCount : 0,
                waiting = outcome is ImportWorkOrdersOutcome.Completed w ? w.WaitingCount : 0
            }));
        });

        return endpoints;
    }
}
