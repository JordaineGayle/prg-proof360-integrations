using System.ComponentModel.DataAnnotations;

namespace PRG.Proof360.Integrations.FieldFlow;

/// <summary>
/// Validated FieldFlow adapter options. Secrets come from configuration/environment only.
/// </summary>
public sealed class FieldFlowOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "FieldFlow";

    /// <summary>Base URL for the FieldFlow API (mock or real).</summary>
    [Required]
    public string BaseUrl { get; set; } = "http://localhost:5210";

    /// <summary>API key sent as <c>X-Api-Key</c>.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>HMAC secret for webhook verification.</summary>
    public string WebhookHmacSecret { get; set; } = string.Empty;

    /// <summary>Configured provider instance identifier.</summary>
    [Required]
    public string ProviderInstanceId { get; set; } = "fieldflow-local-1";

    /// <summary>
    /// Coarse HttpClient timeout ceiling in milliseconds.
    /// Per-attempt timeout and retries are configured under <c>FieldFlow:Resilience</c>.
    /// </summary>
    [Range(100, 600_000)]
    public int TimeoutMilliseconds { get; set; } = 60_000;

    /// <summary>Allowed webhook timestamp skew in seconds (default ±5 minutes).</summary>
    [Range(0, 3600)]
    public int WebhookTimestampSkewSeconds { get; set; } = 300;

    /// <summary>Maximum accepted webhook body size in bytes.</summary>
    [Range(1024, 1_048_576)]
    public int MaxWebhookBodyBytes { get; set; } = 65_536;
}
