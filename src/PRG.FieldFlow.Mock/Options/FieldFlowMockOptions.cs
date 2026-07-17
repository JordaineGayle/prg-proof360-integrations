namespace PRG.FieldFlow.Mock.Options;

/// <summary>
/// Configuration for the local FieldFlow mock. Secrets must come from environment/user-secrets, never committed values.
/// </summary>
public sealed class FieldFlowMockOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "FieldFlowMock";

    /// <summary>Expected API key header value (<c>X-Api-Key</c>).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>HMAC secret used to sign outbound webhook demo payloads.</summary>
    public string WebhookHmacSecret { get; set; } = string.Empty;

    /// <summary>Provider instance identifier included in webhook envelopes.</summary>
    public string ProviderInstanceId { get; set; } = "fieldflow-local-1";

    /// <summary>Maximum request body size in bytes.</summary>
    public int MaxRequestBodyBytes { get; set; } = 64 * 1024;
}
