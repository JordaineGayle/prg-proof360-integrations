namespace PRG.Proof360.Integrations.Core.Providers.Contracts;

/// <summary>
/// Raw inbound webhook verification inputs.
/// </summary>
public sealed class WebhookVerificationRequest
{
    /// <summary>Raw request body bytes (signing input).</summary>
    public required ReadOnlyMemory<byte> RawBody { get; init; }

    /// <summary>Signature header value.</summary>
    public string? Signature { get; init; }

    /// <summary>Unix timestamp header value.</summary>
    public string? TimestampHeader { get; init; }

    /// <summary>Provider instance header value.</summary>
    public string? ProviderInstanceHeader { get; init; }

    /// <summary>Event id header value.</summary>
    public string? EventIdHeader { get; init; }
}
