namespace PRG.Proof360.Integrations.Core.Integration;

/// <summary>
/// Durable inbound receipt and processing record. Not a canonical Proof360 entity.
/// </summary>
public sealed class InboxMessage
{
    /// <summary>Gets or sets Id.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets ProviderName.</summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>Gets or sets ProviderInstanceId.</summary>
    public string ProviderInstanceId { get; set; } = string.Empty;

    /// <summary>Gets or sets EventId.</summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>Gets or sets EventType.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Gets or sets SchemaVersion.</summary>
    public string? SchemaVersion { get; set; }

    /// <summary>Gets or sets EventVersion.</summary>
    public long? EventVersion { get; set; }

    /// <summary>Gets or sets CorrelationId.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Gets or sets CausationId.</summary>
    public string? CausationId { get; set; }

    /// <summary>Gets or sets OccurredAt.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Gets or sets ReceivedAt.</summary>
    public DateTimeOffset ReceivedAt { get; set; }

    /// <summary>Protected payload envelope or raw body stored for replay; never copied into canonical tables.</summary>
    public string PayloadEnvelope { get; set; } = string.Empty;

    /// <summary>Gets or sets PayloadHash.</summary>
    public string PayloadHash { get; set; } = string.Empty;

    /// <summary>Gets or sets State.</summary>
    public string State { get; set; } = InboxMessageStates.Pending;

    /// <summary>Gets or sets AttemptCount.</summary>
    public int AttemptCount { get; set; }

    /// <summary>Gets or sets NextAttemptAt.</summary>
    public DateTimeOffset? NextAttemptAt { get; set; }

    /// <summary>Gets or sets ErrorCategory.</summary>
    public string? ErrorCategory { get; set; }

    /// <summary>Gets or sets ErrorMessage.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Append-only sanitized failure history JSON. Not overwritten on replay; last error fields may clear.
    /// </summary>
    public string? FailureHistoryJson { get; set; }

    /// <summary>Gets or sets RowVersion.</summary>
    public uint RowVersion { get; set; }
}
