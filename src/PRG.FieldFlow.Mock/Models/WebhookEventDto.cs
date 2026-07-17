using System.Text.Json.Serialization;

namespace PRG.FieldFlow.Mock.Models;

/// <summary>
/// Webhook event envelope the mock can send to the connector.
/// </summary>
public sealed class WebhookEventDto
{
    /// <summary>Unique event identifier.</summary>
    [JsonPropertyName("eventId")]
    public string EventId { get; set; } = string.Empty;

    /// <summary>Event type, for example work_order.status_changed.</summary>
    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    /// <summary>Payload schema version.</summary>
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; set; } = "1.0";

    /// <summary>Entity sequence/version for ordering.</summary>
    [JsonPropertyName("entityVersion")]
    public long EntityVersion { get; set; }

    /// <summary>When the event occurred (UTC).</summary>
    [JsonPropertyName("occurredAt")]
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Provider instance that produced the event.</summary>
    [JsonPropertyName("providerInstanceId")]
    public string ProviderInstanceId { get; set; } = string.Empty;

    /// <summary>Work-order payload.</summary>
    [JsonPropertyName("data")]
    public WorkOrderDto? Data { get; set; }
}

/// <summary>Stable error body for validation/auth failures.</summary>
public sealed class ErrorResponse
{
    /// <summary>Stable error code.</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>Sanitized message.</summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
