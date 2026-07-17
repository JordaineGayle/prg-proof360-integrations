using PRG.Proof360.Integrations.Core.Providers.Contracts;

namespace PRG.Proof360.Integrations.Core.Providers.Capabilities;

/// <summary>
/// Verifies inbound provider webhook authenticity.
/// </summary>
public interface IWebhookVerifier
{
    /// <summary>Verifies signature, timestamp skew, and provider instance binding.</summary>
    WebhookVerificationResult Verify(WebhookVerificationRequest request);
}
