using Microsoft.Extensions.Options;
using PRG.Proof360.Integrations.Api.Errors;
using PRG.Proof360.Integrations.Api.Middleware;
using PRG.Proof360.Integrations.Application.Outcomes;
using PRG.Proof360.Integrations.Application.Replay;

namespace PRG.Proof360.Integrations.Api.Endpoints;

/// <summary>
/// Local/admin-only dead-letter replay. Not a production authorization model.
/// </summary>
public static class AdminReplayEndpoints
{
    /// <summary>Maps admin replay routes when enabled.</summary>
    public static IEndpointRouteBuilder MapAdminReplayEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost("/admin/inbox/{inboxMessageId:guid}/replay", async (
            Guid inboxMessageId,
            ReplayRequestBody body,
            ReplayDeadLetterHandler handler,
            IOptions<AdminReplayOptions> options,
            IHostEnvironment environment,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var opts = options.Value;
            if (!opts.Enabled)
            {
                return Results.NotFound();
            }

            // Prototype gate:
            // - OperatorToken configured → require matching X-Admin-Operator-Token
            // - otherwise only Development host may call
            var token = http.Request.Headers["X-Admin-Operator-Token"].ToString();
            if (!string.IsNullOrWhiteSpace(opts.OperatorToken))
            {
                if (!string.Equals(token, opts.OperatorToken, StringComparison.Ordinal))
                {
                    return Results.Unauthorized();
                }
            }
            else if (!environment.IsDevelopment())
            {
                return Results.Unauthorized();
            }

            var result = await handler.HandleAsync(
                new ReplayDeadLetterCommand
                {
                    InboxMessageId = inboxMessageId,
                    OperatorId = body.OperatorId ?? string.Empty,
                    Reason = body.Reason ?? string.Empty,
                    CorrelationId = CorrelationIdMiddleware.GetCorrelationId(http)
                },
                cancellationToken);

            return result.ToHttpResult(http, outcome => outcome switch
            {
                ReplayOutcome.Accepted accepted => Results.Accepted(
                    $"/admin/inbox/{accepted.InboxMessageId}/replay",
                    new { inboxMessageId = accepted.InboxMessageId, status = "requeued" }),
                ReplayOutcome.AlreadyComplete complete => Results.Ok(
                    new { inboxMessageId = complete.InboxMessageId, status = "already_complete" }),
                _ => Results.Ok(outcome)
            });
        })
        .WithTags("Admin")
        .WithSummary("Replay a dead-lettered inbox message (operator-gated)");

        return endpoints;
    }

    /// <summary>Replay request body.</summary>
    public sealed class ReplayRequestBody
    {
        /// <summary>Operator identity.</summary>
        public string? OperatorId { get; set; }

        /// <summary>Reason for replay.</summary>
        public string? Reason { get; set; }
    }
}
