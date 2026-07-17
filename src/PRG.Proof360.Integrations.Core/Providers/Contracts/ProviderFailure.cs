namespace PRG.Proof360.Integrations.Core.Providers.Contracts;

/// <summary>
/// Sanitized provider failure returned from capability ports.
/// Never carries raw response bodies, credentials, or PII.
/// </summary>
/// <param name="Kind">Operational classification.</param>
/// <param name="Code">Stable machine-readable code.</param>
/// <param name="SafeMessage">Safe human-readable message.</param>
/// <param name="RetryAfter">Optional retry delay from a trustworthy Retry-After.</param>
/// <param name="StatusCode">Optional HTTP status when known (adapter-only metadata).</param>
public sealed record ProviderFailure(
    ProviderFailureKind Kind,
    string Code,
    string SafeMessage,
    TimeSpan? RetryAfter = null,
    int? StatusCode = null)
{
    /// <summary>Creates a validation failure.</summary>
    public static ProviderFailure Validation(string code, string safeMessage) =>
        new(ProviderFailureKind.Validation, code, safeMessage);

    /// <summary>Creates an unsupported-capability failure.</summary>
    public static ProviderFailure Unsupported(string safeMessage) =>
        new(ProviderFailureKind.ContractViolation, "unsupported_capability", safeMessage);
}
