namespace PRG.Proof360.Integrations.Core.Providers.Contracts;

/// <summary>
/// Outcome of webhook authenticity checks.
/// </summary>
/// <param name="IsValid">Whether the webhook passed verification.</param>
/// <param name="FailureCode">Stable failure code when invalid.</param>
/// <param name="FailureMessage">Sanitized failure reason.</param>
public sealed record WebhookVerificationResult(bool IsValid, string? FailureCode = null, string? FailureMessage = null)
{
    /// <summary>Valid signature/timestamp/instance.</summary>
    public static WebhookVerificationResult Valid() => new(true);

    /// <summary>Invalid verification.</summary>
    public static WebhookVerificationResult Invalid(string code, string message) => new(false, code, message);
}
