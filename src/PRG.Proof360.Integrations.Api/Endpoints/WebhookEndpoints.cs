using Microsoft.Extensions.Options;
using PRG.Proof360.Integrations.Api.Errors;
using PRG.Proof360.Integrations.Api.Middleware;
using PRG.Proof360.Integrations.Application.Errors;
using PRG.Proof360.Integrations.Application.Inbox;
using PRG.Proof360.Integrations.Application.Outcomes;
using PRG.Proof360.Integrations.FieldFlow;

namespace PRG.Proof360.Integrations.Api.Endpoints;

/// <summary>
/// FieldFlow webhook receive endpoint. Verifies and durably stores; does not process.
/// </summary>
public static class WebhookEndpoints
{
    /// <summary>Maps webhook routes.</summary>
    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/webhooks/events", async (
            HttpContext http,
            ReceiveWebhookEventHandler handler,
            IOptions<FieldFlowOptions> options,
            CancellationToken cancellationToken) =>
        {
            var maxBytes = Math.Max(1024, options.Value.MaxWebhookBodyBytes);
            if (http.Request.ContentLength is { } length && length > maxBytes)
            {
                var tooLarge = new IntegrationFailure(
                    FailureCodes.WebhookPayloadTooLarge,
                    "Webhook body exceeds the configured size limit.",
                    FailureCategory.Validation);
                var problem = ProblemDetailsMapper.ToProblemDetails(
                    tooLarge,
                    CorrelationIdMiddleware.GetCorrelationId(http));
                return Results.Json(problem, statusCode: 413, contentType: "application/problem+json");
            }

            // Read raw body exactly once — do not deserialize/reserialize before verification.
            http.Request.EnableBuffering();
            using var ms = new MemoryStream(capacity: (int)Math.Min(maxBytes + 1, 131_072));
            await http.Request.Body.CopyToAsync(ms, cancellationToken);
            if (ms.Length > maxBytes)
            {
                var tooLarge = new IntegrationFailure(
                    FailureCodes.WebhookPayloadTooLarge,
                    "Webhook body exceeds the configured size limit.",
                    FailureCategory.Validation);
                var problem = ProblemDetailsMapper.ToProblemDetails(
                    tooLarge,
                    CorrelationIdMiddleware.GetCorrelationId(http));
                return Results.Json(problem, statusCode: 413, contentType: "application/problem+json");
            }

            var rawBody = ms.ToArray();
            var correlationId = CorrelationIdMiddleware.GetCorrelationId(http);

            var result = await handler.HandleAsync(
                new ReceiveWebhookEventCommand
                {
                    RawBody = rawBody,
                    SignatureHeader = Header(http, "X-FieldFlow-Signature"),
                    TimestampHeader = Header(http, "X-FieldFlow-Timestamp"),
                    ProviderInstanceHeader = Header(http, "X-FieldFlow-Provider-Instance"),
                    EventIdHeader = Header(http, "X-FieldFlow-Event-Id"),
                    EventTypeHeader = Header(http, "X-FieldFlow-Event-Type"),
                    SchemaVersionHeader = Header(http, "X-FieldFlow-Schema-Version"),
                    EntityVersionHeader = Header(http, "X-FieldFlow-Entity-Version"),
                    CorrelationId = correlationId,
                    MaxBodyBytes = maxBytes
                },
                cancellationToken);

            return result.ToHttpResult(http, outcome =>
            {
                var inboxId = outcome switch
                {
                    ReceiveEventOutcome.Accepted a => a.InboxMessageId,
                    ReceiveEventOutcome.Duplicate d => d.ExistingInboxMessageId,
                    _ => Guid.Empty
                };

                var duplicate = outcome is ReceiveEventOutcome.Duplicate;
                return Results.Json(
                    new
                    {
                        status = duplicate ? "duplicate" : "accepted",
                        inboxMessageId = inboxId,
                        correlationId
                    },
                    statusCode: StatusCodes.Status202Accepted);
            });
        })
        .WithTags("Webhooks")
        .WithSummary("Receive HMAC-signed FieldFlow webhook (accept to inbox; do not process inline)");

        return endpoints;
    }

    private static string? Header(HttpContext http, string name) =>
        http.Request.Headers.TryGetValue(name, out var values) ? values.ToString() : null;
}
