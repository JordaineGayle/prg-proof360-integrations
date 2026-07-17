using PRG.Proof360.Integrations.Core.Providers.Contracts;

namespace PRG.Proof360.Integrations.Application.Errors;

/// <summary>
/// Translates provider-port failures into application <see cref="IntegrationFailure"/> values.
/// </summary>
public static class ProviderFailureTranslator
{
    /// <summary>
    /// Maps a <see cref="ProviderFailure"/> to an <see cref="IntegrationFailure"/>.
    /// Preserves the provider code for audit via <see cref="IntegrationFailure.ProviderCode"/>.
    /// </summary>
    public static IntegrationFailure ToIntegrationFailure(ProviderFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        var (category, code) = failure.Kind switch
        {
            ProviderFailureKind.Validation => (FailureCategory.Validation, MapValidationCode(failure.Code)),
            ProviderFailureKind.Authentication => (FailureCategory.ProviderAuthentication, FailureCodes.ProviderAuthenticationFailed),
            ProviderFailureKind.Forbidden => (FailureCategory.ProviderAuthentication, FailureCodes.ProviderForbidden),
            ProviderFailureKind.RateLimited => (FailureCategory.RateLimited, FailureCodes.ProviderRateLimited),
            ProviderFailureKind.Timeout => (FailureCategory.Timeout, FailureCodes.ProviderTimeout),
            ProviderFailureKind.Unavailable => (FailureCategory.Unavailable, FailureCodes.ProviderUnavailable),
            ProviderFailureKind.CircuitOpen => (FailureCategory.Unavailable, FailureCodes.CircuitOpen),
            ProviderFailureKind.ContractViolation => (FailureCategory.ProviderContract, MapContractCode(failure.Code)),
            ProviderFailureKind.AmbiguousWrite => (FailureCategory.Timeout, FailureCodes.AmbiguousProviderWrite),
            ProviderFailureKind.NotFound => (FailureCategory.NotFound, FailureCodes.ProviderNotFound),
            ProviderFailureKind.Conflict => (FailureCategory.Conflict, FailureCodes.ProviderConflict),
            _ => (FailureCategory.Unexpected, FailureCodes.UnexpectedError)
        };

        return new IntegrationFailure(
            code,
            failure.SafeMessage,
            category,
            failure.RetryAfter,
            ProviderCode: failure.Code);
    }

    private static string MapValidationCode(string providerCode) =>
        providerCode switch
        {
            "invalid_date" or "invalid_timestamp" => FailureCodes.InvalidTimestamp,
            "missing_contractor_id" or "missing_work_order_id" or "missing_status"
                or "client_reference_required" or "idempotency_key_required" => FailureCodes.RequiredFieldMissing,
            _ => FailureCodes.ValidationFailed
        };

    private static string MapContractCode(string providerCode) =>
        providerCode switch
        {
            "unsupported_capability" => FailureCodes.UnsupportedCapability,
            "unsupported_schema" => FailureCodes.UnsupportedSchema,
            "unknown_status" => FailureCodes.UnknownProviderStatus,
            _ => FailureCodes.MalformedProviderPayload
        };
}
