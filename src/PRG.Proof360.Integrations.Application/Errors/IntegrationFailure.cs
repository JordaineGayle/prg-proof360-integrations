namespace PRG.Proof360.Integrations.Application.Errors;

/// <summary>
/// Application-boundary failure contract. Safe for Problem Details, audit, and worker disposition.
/// </summary>
/// <param name="Code">Stable machine-readable code.</param>
/// <param name="SafeMessage">Safe human-readable message (no secrets/PII/raw bodies).</param>
/// <param name="Category">Failure category.</param>
/// <param name="RetryAfter">Optional bounded retry delay.</param>
/// <param name="ValidationErrors">Optional field validation errors.</param>
/// <param name="ProviderCode">Optional original sanitized provider code for audit.</param>
public sealed record IntegrationFailure(
    string Code,
    string SafeMessage,
    FailureCategory Category,
    TimeSpan? RetryAfter = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null,
    string? ProviderCode = null)
{
    /// <summary>Creates a validation failure.</summary>
    public static IntegrationFailure Validation(
        string code,
        string safeMessage,
        IReadOnlyDictionary<string, string[]>? validationErrors = null) =>
        new(code, safeMessage, FailureCategory.Validation, ValidationErrors: validationErrors);

    /// <summary>Creates an unexpected sanitized failure.</summary>
    public static IntegrationFailure Unexpected(string safeMessage = "An unexpected error occurred.") =>
        new(FailureCodes.UnexpectedError, safeMessage, FailureCategory.Unexpected);
}
