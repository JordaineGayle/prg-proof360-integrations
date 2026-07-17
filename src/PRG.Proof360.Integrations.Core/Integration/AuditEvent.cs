namespace PRG.Proof360.Integrations.Core.Integration;

/// <summary>
/// Append-only operational audit record. Contains sanitized categories and hashes only—
/// never authorization headers, signatures, or raw webhook bodies.
/// </summary>
public sealed class AuditEvent
{
    /// <summary>Gets or sets Id.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets CorrelationId.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Gets or sets CausationId.</summary>
    public string? CausationId { get; set; }

    /// <summary>Gets or sets Direction.</summary>
    public string Direction { get; set; } = string.Empty;

    /// <summary>Gets or sets ProviderName.</summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>Gets or sets ProviderInstanceId.</summary>
    public string? ProviderInstanceId { get; set; }

    /// <summary>Gets or sets Operation.</summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>Gets or sets CanonicalEntityType.</summary>
    public string? CanonicalEntityType { get; set; }

    /// <summary>Gets or sets CanonicalId.</summary>
    public Guid? CanonicalId { get; set; }

    /// <summary>Gets or sets EventId.</summary>
    public string? EventId { get; set; }

    /// <summary>Gets or sets Attempt.</summary>
    public int Attempt { get; set; }

    /// <summary>Gets or sets Result.</summary>
    public string Result { get; set; } = string.Empty;

    /// <summary>Gets or sets ErrorCategory.</summary>
    public string? ErrorCategory { get; set; }

    /// <summary>Gets or sets LatencyMilliseconds.</summary>
    public long? LatencyMilliseconds { get; set; }

    /// <summary>Gets or sets SchemaVersion.</summary>
    public string? SchemaVersion { get; set; }

    /// <summary>Gets or sets PayloadHash.</summary>
    public string? PayloadHash { get; set; }

    /// <summary>Gets or sets Timestamp.</summary>
    public DateTimeOffset Timestamp { get; set; }
}
