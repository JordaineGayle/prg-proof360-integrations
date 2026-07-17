using System.Net;
using System.Text.Json;
using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.FieldFlow.Contracts;

namespace PRG.Proof360.Integrations.FieldFlow;

/// <summary>
/// Classifies FieldFlow HTTP failures into <see cref="ProviderFailure"/> once at the adapter boundary.
/// Does not expose raw sensitive response bodies.
/// </summary>
public static class FieldFlowErrorClassifier
{
    /// <summary>
    /// Maps an HTTP status and optional sanitized error JSON to a <see cref="ProviderFailure"/>.
    /// </summary>
    public static ProviderFailure Classify(HttpStatusCode statusCode, string? responseBody, TimeSpan? retryAfter = null)
    {
        var code = "provider_error";
        var message = "FieldFlow request failed.";

        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            try
            {
                var error = JsonSerializer.Deserialize<FieldFlowErrorDto>(responseBody);
                if (!string.IsNullOrWhiteSpace(error?.Code))
                {
                    code = error.Code!;
                }

                if (!string.IsNullOrWhiteSpace(error?.Message))
                {
                    message = error.Message!.Length > 200 ? error.Message[..200] : error.Message;
                }
            }
            catch (JsonException)
            {
                // Ignore unparsable bodies.
            }
        }

        var kind = statusCode switch
        {
            HttpStatusCode.Unauthorized => ProviderFailureKind.Authentication,
            HttpStatusCode.Forbidden => ProviderFailureKind.Forbidden,
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => ProviderFailureKind.Validation,
            HttpStatusCode.NotFound => ProviderFailureKind.NotFound,
            HttpStatusCode.Conflict => ProviderFailureKind.Conflict,
            HttpStatusCode.TooManyRequests => ProviderFailureKind.RateLimited,
            HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => ProviderFailureKind.Timeout,
            HttpStatusCode.ServiceUnavailable => ProviderFailureKind.Unavailable,
            >= HttpStatusCode.InternalServerError => ProviderFailureKind.Unavailable,
            _ => ProviderFailureKind.ContractViolation
        };

        return new ProviderFailure(kind, code, message, retryAfter, (int)statusCode);
    }

    /// <summary>
    /// Classifies transport failures. Caller cancellation must be rethrown by the caller, not passed here.
    /// </summary>
    public static ProviderFailure FromTransportException(Exception exception) =>
        exception is TimeoutException
            ? new ProviderFailure(ProviderFailureKind.Timeout, "timeout", "FieldFlow request timed out.")
            : new ProviderFailure(ProviderFailureKind.Unavailable, "transport_error", "FieldFlow transport failure.");
}
