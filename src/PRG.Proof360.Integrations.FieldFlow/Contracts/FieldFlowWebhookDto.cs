using System.Text.Json.Serialization;

namespace PRG.Proof360.Integrations.FieldFlow.Contracts;

/// <summary>
/// FieldFlow webhook envelope body matching the mock contract.
/// </summary>
public sealed class FieldFlowWebhookDto
{
    /// <summary>Event id.</summary>
    [JsonPropertyName("eventId")]
    public string? EventId { get; set; }

    /// <summary>Event type.</summary>
    [JsonPropertyName("eventType")]
    public string? EventType { get; set; }

    /// <summary>Schema version.</summary>
    [JsonPropertyName("schemaVersion")]
    public string? SchemaVersion { get; set; }

    /// <summary>Entity version.</summary>
    [JsonPropertyName("entityVersion")]
    public long EntityVersion { get; set; }

    /// <summary>Occurred at UTC.</summary>
    [JsonPropertyName("occurredAt")]
    public DateTimeOffset? OccurredAt { get; set; }

    /// <summary>Provider instance id.</summary>
    [JsonPropertyName("providerInstanceId")]
    public string? ProviderInstanceId { get; set; }

    /// <summary>Work-order payload.</summary>
    [JsonPropertyName("data")]
    public FieldFlowWorkOrderDto? Data { get; set; }
}
