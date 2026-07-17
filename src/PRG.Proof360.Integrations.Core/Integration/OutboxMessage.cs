namespace PRG.Proof360.Integrations.Core.Integration;

/// <summary>
/// Durable outbound intent record. Not a canonical Proof360 entity.
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>Gets or sets Id.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets ProviderName.</summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>Gets or sets ProviderInstanceId.</summary>
    public string ProviderInstanceId { get; set; } = string.Empty;

    /// <summary>Gets or sets OperationType.</summary>
    public string OperationType { get; set; } = string.Empty;

    /// <summary>Gets or sets IdempotencyKey.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>Gets or sets CanonicalEntityType.</summary>
    public string CanonicalEntityType { get; set; } = string.Empty;

    /// <summary>Gets or sets CanonicalId.</summary>
    public Guid CanonicalId { get; set; }

    /// <summary>Gets or sets ExpectedCanonicalVersion.</summary>
    public long? ExpectedCanonicalVersion { get; set; }

    /// <summary>Gets or sets CommandPayload.</summary>
    public string CommandPayload { get; set; } = string.Empty;

    /// <summary>Gets or sets State.</summary>
    public string State { get; set; } = OutboxMessageStates.Pending;

    /// <summary>Gets or sets AttemptCount.</summary>
    public int AttemptCount { get; set; }

    /// <summary>Gets or sets NextAttemptAt.</summary>
    public DateTimeOffset? NextAttemptAt { get; set; }

    /// <summary>Gets or sets ResultReference.</summary>
    public string? ResultReference { get; set; }

    /// <summary>Gets or sets ErrorCategory.</summary>
    public string? ErrorCategory { get; set; }

    /// <summary>Gets or sets ErrorMessage.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Append-only sanitized failure history JSON. Not overwritten on replay; last error fields may clear.
    /// </summary>
    public string? FailureHistoryJson { get; set; }

    /// <summary>Gets or sets CreatedAt.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets RowVersion.</summary>
    public uint RowVersion { get; set; }
}
