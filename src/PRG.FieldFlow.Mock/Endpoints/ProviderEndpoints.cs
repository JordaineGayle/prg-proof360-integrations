using Microsoft.Extensions.Options;
using PRG.FieldFlow.Mock.Models;
using PRG.FieldFlow.Mock.Options;
using PRG.FieldFlow.Mock.State;

namespace PRG.FieldFlow.Mock.Endpoints;

/// <summary>
/// Normal FieldFlow provider endpoints used by the connector.
/// </summary>
public static class ProviderEndpoints
{
    /// <summary>Maps provider routes.</summary>
    public static IEndpointRouteBuilder MapProviderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/contractors", (MockStore store) => Results.Ok(store.ListContractors()));

        endpoints.MapGet("/work-orders", (MockStore store) => Results.Ok(store.ListWorkOrders()));

        endpoints.MapGet("/work-orders/{id}", (string id, MockStore store) =>
        {
            var workOrder = store.FindWorkOrder(id);
            return workOrder is null
                ? Results.Json(new ErrorResponse { Code = "not_found", Message = "Work order not found." }, statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(workOrder);
        });

        endpoints.MapPost("/work-orders", async (HttpContext http, MockStore store) =>
        {
            if (!http.Request.Headers.TryGetValue("Idempotency-Key", out var keyValues) ||
                string.IsNullOrWhiteSpace(keyValues.ToString()))
            {
                return Results.Json(
                    new ErrorResponse { Code = "idempotency_key_required", Message = "Idempotency-Key header is required." },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var request = await http.Request.ReadFromJsonAsync<CreateWorkOrderRequest>();
            if (request is null)
            {
                return Results.Json(
                    new ErrorResponse { Code = "invalid_body", Message = "Request body is required." },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var validation = ValidateCreate(request);
            if (validation is not null)
            {
                return Results.Json(validation, statusCode: StatusCodes.Status400BadRequest);
            }

            var result = store.CreateWorkOrder(keyValues.ToString(), request);
            if (result.StatusCode == StatusCodes.Status409Conflict)
            {
                return Results.Json(
                    new ErrorResponse { Code = "idempotency_conflict", Message = "Idempotency-Key was reused with a different request." },
                    statusCode: StatusCodes.Status409Conflict);
            }

            if (store.Failures.ConsumeAmbiguousPost())
            {
                // Persist succeeded above; drop the connection so the client must reconcile.
                return new DroppedResponseResult();
            }

            return Results.Json(result.WorkOrder, statusCode: result.StatusCode);
        });

        endpoints.MapPatch("/work-orders/{id}/status", async (string id, HttpContext http, MockStore store) =>
        {
            var request = await http.Request.ReadFromJsonAsync<PatchStatusRequest>();
            if (request is null || string.IsNullOrWhiteSpace(request.Status))
            {
                return Results.Json(
                    new ErrorResponse { Code = "invalid_status", Message = "status is required." },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (!IsKnownStatus(request.Status))
            {
                return Results.Json(
                    new ErrorResponse { Code = "unknown_status", Message = "status is not recognized." },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var updated = store.PatchStatus(id, request);
            return updated is null
                ? Results.Json(new ErrorResponse { Code = "not_found", Message = "Work order not found." }, statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(updated);
        });

        endpoints.MapGet("/health", (MockStore store, IOptions<FieldFlowMockOptions> options) =>
        {
            if (store.Failures.HealthUnavailable)
            {
                return Results.Json(
                    new { status = "Unavailable", service = "PRG.FieldFlow.Mock" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Ok(new
            {
                status = "Healthy",
                service = "PRG.FieldFlow.Mock",
                providerInstanceId = options.Value.ProviderInstanceId,
                utc = DateTimeOffset.UtcNow
            });
        });

        return endpoints;
    }

    private static ErrorResponse? ValidateCreate(CreateWorkOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ClientReference))
        {
            return new ErrorResponse { Code = "client_reference_required", Message = "clientReference is required." };
        }

        if (string.IsNullOrWhiteSpace(request.CustomerName))
        {
            return new ErrorResponse { Code = "customer_name_required", Message = "customerName is required." };
        }

        if (string.IsNullOrWhiteSpace(request.AddressStreet) || string.IsNullOrWhiteSpace(request.AddressCity))
        {
            return new ErrorResponse { Code = "address_required", Message = "addressStreet and addressCity are required." };
        }

        if (string.IsNullOrWhiteSpace(request.ServiceType))
        {
            return new ErrorResponse { Code = "service_type_required", Message = "serviceType is required." };
        }

        return null;
    }

    private static bool IsKnownStatus(string status) =>
        status is WorkOrderStatuses.Open
            or WorkOrderStatuses.Scheduled
            or WorkOrderStatuses.InProgress
            or WorkOrderStatuses.Done
            or WorkOrderStatuses.Void;

    /// <summary>Aborts the HTTP connection after a successful persist (ambiguous POST).</summary>
    private sealed class DroppedResponseResult : IResult
    {
        /// <inheritdoc />
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Abort();
            return Task.CompletedTask;
        }
    }
}
