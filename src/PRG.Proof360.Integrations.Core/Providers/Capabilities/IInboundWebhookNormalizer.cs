using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Core.Results;

namespace PRG.Proof360.Integrations.Core.Providers.Capabilities;

/// <summary>
/// Maps a verified webhook body into a provider-neutral inbox envelope.
/// Must only be called after signature verification succeeds.
/// </summary>
public interface IInboundWebhookNormalizer
{
    /// <summary>Normalizes a verified webhook into an inbox-ready event.</summary>
    Task<Result<NormalizedWebhookEvent, ProviderFailure>> NormalizeAsync(
        WebhookNormalizeRequest request,
        CancellationToken cancellationToken);
}
