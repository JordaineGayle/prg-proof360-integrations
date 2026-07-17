using PRG.Proof360.Integrations.Api.Errors;
using PRG.Proof360.Integrations.Api.Middleware;
using PRG.Proof360.Integrations.Application.Dispatch;

namespace PRG.Proof360.Integrations.Api.Endpoints;

/// <summary>
/// Qualified Job dispatch endpoint. Application owns eligibility/outbox; no EF/provider DTOs.
/// </summary>
public static class DispatchEndpoints
{
    /// <summary>Maps dispatch routes.</summary>
    public static IEndpointRouteBuilder MapDispatchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/connectors/fieldflow/jobs/{jobId:guid}/dispatch", async (
            Guid jobId,
            QueueJobDispatchHandler handler,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var correlationId = CorrelationIdMiddleware.GetCorrelationId(http);
            var result = await handler.HandleAsync(jobId, correlationId, cancellationToken);
            return result.ToHttpResult(http, outcome =>
            {
                return outcome switch
                {
                    QueueDispatchOutcome.Queued q => Results.Accepted(
                        $"/connectors/fieldflow/jobs/{jobId}/dispatch",
                        new
                        {
                            status = "queued",
                            outboxMessageId = q.OutboxMessageId,
                            idempotencyKey = q.IdempotencyKey,
                            correlationId
                        }),
                    QueueDispatchOutcome.AlreadyQueued a => Results.Accepted(
                        $"/connectors/fieldflow/jobs/{jobId}/dispatch",
                        new
                        {
                            status = "already_queued",
                            outboxMessageId = a.OutboxMessageId,
                            outboxState = a.State,
                            idempotencyKey = a.IdempotencyKey,
                            correlationId
                        }),
                    QueueDispatchOutcome.AlreadyDispatched d => Results.Ok(new
                    {
                        status = "already_dispatched",
                        jobId = d.JobId,
                        externalWorkOrderId = d.ExternalWorkOrderId,
                        correlationId
                    }),
                    _ => Results.Ok(new { status = "unknown", correlationId })
                };
            });
        })
        .WithTags("Dispatch")
        .WithSummary("Queue outbound FieldFlow dispatch for a qualified Job");

        return endpoints;
    }
}
