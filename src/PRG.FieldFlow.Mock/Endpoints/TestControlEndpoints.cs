using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PRG.FieldFlow.Mock.Models;
using PRG.FieldFlow.Mock.Options;
using PRG.FieldFlow.Mock.Security;
using PRG.FieldFlow.Mock.State;

namespace PRG.FieldFlow.Mock.Endpoints;

/// <summary>
/// Local-only test/demo controls under <c>/_test</c>. Not part of the provider contract surface.
/// </summary>
public static class TestControlEndpoints
{
    /// <summary>Maps test control routes.</summary>
    public static IEndpointRouteBuilder MapTestControlEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/_test");

        group.MapPost("/reset", (MockStore store) =>
        {
            store.Reset();
            return Results.Ok(new { reset = true });
        });

        group.MapPost("/contractors", (ContractorDto contractor, MockStore store) =>
        {
            if (string.IsNullOrWhiteSpace(contractor.ContractorId))
            {
                return Results.Json(
                    new ErrorResponse { Code = "contractor_id_required", Message = "contractorId is required." },
                    statusCode: 400);
            }

            var saved = store.UpsertContractor(contractor);
            return Results.Ok(new { upserted = true, contractorId = saved.ContractorId });
        });

        group.MapPost("/work-orders", (WorkOrderDto workOrder, MockStore store) =>
        {
            if (string.IsNullOrWhiteSpace(workOrder.WorkOrderId))
            {
                return Results.Json(
                    new ErrorResponse { Code = "work_order_id_required", Message = "workOrderId is required." },
                    statusCode: 400);
            }

            var saved = store.UpsertWorkOrder(workOrder);
            return Results.Ok(new { upserted = true, workOrderId = saved.WorkOrderId });
        });

        group.MapPost("/failures", (FailureInjectionRequest request, MockStore store) =>
        {
            store.Failures.Configure(request);
            return Results.Ok(new
            {
                store.Failures.Remaining429,
                store.Failures.Remaining500,
                store.Failures.RemainingTimeouts,
                store.Failures.HealthUnavailable,
                ambiguousArmed = request.AmbiguousNextPost == true
            });
        });

        group.MapGet("/work-orders/by-client-ref/{clientReference}", (string clientReference, MockStore store) =>
        {
            var workOrder = store.FindByClientReference(clientReference);
            return workOrder is null
                ? Results.Json(new ErrorResponse { Code = "not_found", Message = "No work order for client reference." }, statusCode: 404)
                : Results.Ok(workOrder);
        });

        group.MapGet("/webhooks/emitted", (MockStore store) => Results.Ok(store.EmittedWebhooks()));

        group.MapPost("/webhooks/build", (BuildWebhookRequest request, MockStore store, IOptions<FieldFlowMockOptions> options) =>
        {
            var workOrder = store.FindWorkOrder(request.WorkOrderId ?? string.Empty);
            if (workOrder is null)
            {
                return Results.Json(new ErrorResponse { Code = "not_found", Message = "Work order not found." }, statusCode: 404);
            }

            if (request.EntityVersion is not null)
            {
                workOrder.EntityVersion = request.EntityVersion.Value;
            }

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                workOrder.Status = request.Status;
            }

            var eventId = string.IsNullOrWhiteSpace(request.EventId) ? $"evt-{Guid.NewGuid():N}" : request.EventId!;
            var evt = store.BuildWebhook(
                eventId,
                request.EventType ?? "work_order.status_changed",
                workOrder,
                options.Value.ProviderInstanceId,
                request.OccurredAt);

            var raw = JsonSerializer.SerializeToUtf8Bytes(evt);
            var unix = evt.OccurredAt.ToUnixTimeSeconds();
            var signature = WebhookSigner.Sign(options.Value.WebhookHmacSecret, unix, raw);

            return Results.Ok(new
            {
                headers = new Dictionary<string, string>
                {
                    ["X-FieldFlow-Provider-Instance"] = options.Value.ProviderInstanceId,
                    ["X-FieldFlow-Event-Id"] = evt.EventId,
                    ["X-FieldFlow-Event-Type"] = evt.EventType,
                    ["X-FieldFlow-Schema-Version"] = evt.SchemaVersion,
                    ["X-FieldFlow-Entity-Version"] = evt.EntityVersion.ToString(),
                    ["X-FieldFlow-Timestamp"] = unix.ToString(),
                    ["X-FieldFlow-Signature"] = signature
                },
                body = evt,
                rawBodyBase64 = Convert.ToBase64String(raw),
                signingString = $"{unix}.<raw-body-bytes>"
            });
        });

        group.MapPost("/webhooks/send", async (SendWebhookRequest request, MockStore store, IOptions<FieldFlowMockOptions> options, IHttpClientFactory httpClientFactory) =>
        {
            if (string.IsNullOrWhiteSpace(request.TargetUrl))
            {
                return Results.Json(new ErrorResponse { Code = "target_required", Message = "targetUrl is required." }, statusCode: 400);
            }

            var workOrder = store.FindWorkOrder(request.WorkOrderId ?? string.Empty);
            if (workOrder is null)
            {
                return Results.Json(new ErrorResponse { Code = "not_found", Message = "Work order not found." }, statusCode: 404);
            }

            var eventId = string.IsNullOrWhiteSpace(request.EventId) ? $"evt-{Guid.NewGuid():N}" : request.EventId!;
            var evt = store.BuildWebhook(
                eventId,
                request.EventType ?? "work_order.status_changed",
                workOrder,
                options.Value.ProviderInstanceId,
                request.OccurredAt);

            var raw = JsonSerializer.SerializeToUtf8Bytes(evt);
            var unix = evt.OccurredAt.ToUnixTimeSeconds();
            var signature = WebhookSigner.Sign(options.Value.WebhookHmacSecret, unix, raw);

            var client = httpClientFactory.CreateClient("webhook-demo");
            using var message = new HttpRequestMessage(HttpMethod.Post, request.TargetUrl)
            {
                Content = new ByteArrayContent(raw)
            };
            message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            message.Headers.TryAddWithoutValidation("X-FieldFlow-Provider-Instance", options.Value.ProviderInstanceId);
            message.Headers.TryAddWithoutValidation("X-FieldFlow-Event-Id", evt.EventId);
            message.Headers.TryAddWithoutValidation("X-FieldFlow-Event-Type", evt.EventType);
            message.Headers.TryAddWithoutValidation("X-FieldFlow-Schema-Version", evt.SchemaVersion);
            message.Headers.TryAddWithoutValidation("X-FieldFlow-Entity-Version", evt.EntityVersion.ToString());
            message.Headers.TryAddWithoutValidation("X-FieldFlow-Timestamp", unix.ToString());
            message.Headers.TryAddWithoutValidation("X-FieldFlow-Signature", signature);

            try
            {
                using var response = await client.SendAsync(message);
                return Results.Ok(new { delivered = true, statusCode = (int)response.StatusCode, eventId });
            }
            catch (Exception ex)
            {
                return Results.Json(
                    new ErrorResponse { Code = "delivery_failed", Message = $"Webhook delivery failed: {ex.GetType().Name}" },
                    statusCode: 502);
            }
        });

        return endpoints;
    }
}

/// <summary>Build webhook fixture request.</summary>
public class BuildWebhookRequest
{
    /// <summary>Work order id.</summary>
    public string? WorkOrderId { get; set; }

    /// <summary>Optional event id for repeatable duplicates.</summary>
    public string? EventId { get; set; }

    /// <summary>Event type.</summary>
    public string? EventType { get; set; }

    /// <summary>Optional status override on the payload copy.</summary>
    public string? Status { get; set; }

    /// <summary>Optional entity version override for out-of-order fixtures.</summary>
    public long? EntityVersion { get; set; }

    /// <summary>Optional occurred-at override.</summary>
    public DateTimeOffset? OccurredAt { get; set; }
}

/// <summary>Send signed webhook to a connector URL.</summary>
public sealed class SendWebhookRequest : BuildWebhookRequest
{
    /// <summary>Connector webhook URL, for example http://localhost:5203/webhooks/events.</summary>
    public string? TargetUrl { get; set; }
}
