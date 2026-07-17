namespace PRG.Proof360.Integrations.Core.Providers.Contracts;

/// <summary>
/// Provider-neutral webhook event after signature verification and ACL normalization.
/// </summary>
public sealed class NormalizedWebhookEvent
{
    /// <summary>Provider name.</summary>
    public required string ProviderName { get; init; }

    /// <summary>Provider instance id.</summary>
    public required string ProviderInstanceId { get; init; }

    /// <summary>Durable event id.</summary>
    public required string EventId { get; init; }

    /// <summary>Original provider event type (e.g. work_order.status_changed).</summary>
    public required string OriginalEventType { get; init; }

    /// <summary>Inbox event type used for processing dispatch.</summary>
    public required string InboxEventType { get; init; }

    /// <summary>Schema version.</summary>
    public string? SchemaVersion { get; init; }

    /// <summary>Entity version when known.</summary>
    public long? EntityVersion { get; init; }

    /// <summary>Occurred-at UTC.</summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>JSON envelope for inbox storage (snapshot JSON when supported).</summary>
    public required string PayloadEnvelope { get; init; }

    /// <summary>Payload hash (never the raw signature).</summary>
    public required string PayloadHash { get; init; }

    /// <summary>Whether the event type/schema is supported for apply.</summary>
    public bool IsSupported { get; init; }

    /// <summary>When true, Application should reconcile via provider GET before apply.</summary>
    public bool RequiresReconciliation { get; init; }

    /// <summary>External work-order id when known (for reconcile).</summary>
    public string? ExternalWorkOrderId { get; init; }
}

/// <summary>
/// Inputs for webhook normalization (post-verification).
/// </summary>
public sealed class WebhookNormalizeRequest
{
    /// <summary>Raw body bytes (already verified).</summary>
    public required ReadOnlyMemory<byte> RawBody { get; init; }

    /// <summary>Event id header.</summary>
    public string? EventIdHeader { get; init; }

    /// <summary>Event type header.</summary>
    public string? EventTypeHeader { get; init; }

    /// <summary>Schema version header.</summary>
    public string? SchemaVersionHeader { get; init; }

    /// <summary>Entity version header.</summary>
    public string? EntityVersionHeader { get; init; }

    /// <summary>Provider instance header.</summary>
    public string? ProviderInstanceHeader { get; init; }
}
