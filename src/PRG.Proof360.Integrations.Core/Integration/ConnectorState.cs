namespace PRG.Proof360.Integrations.Core.Integration;

/// <summary>
/// Projection used for connector health and sync freshness. Circuit process state may also be in-memory;
/// this row is the durable health-facing checkpoint.
/// </summary>
public sealed class ConnectorState
{
    /// <summary>Gets or sets Id.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets ProviderName.</summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>Gets or sets ProviderInstanceId.</summary>
    public string ProviderInstanceId { get; set; } = string.Empty;

    /// <summary>Gets or sets LastSuccessfulSyncAt.</summary>
    public DateTimeOffset? LastSuccessfulSyncAt { get; set; }

    /// <summary>Gets or sets LastCheckpoint.</summary>
    public string? LastCheckpoint { get; set; }

    /// <summary>Gets or sets CircuitState.</summary>
    public string CircuitState { get; set; } = "Closed";

    /// <summary>Gets or sets InboxBacklogCount.</summary>
    public int InboxBacklogCount { get; set; }

    /// <summary>Gets or sets OutboxBacklogCount.</summary>
    public int OutboxBacklogCount { get; set; }

    /// <summary>Gets or sets DeadLetterCount.</summary>
    public int DeadLetterCount { get; set; }

    /// <summary>Gets or sets LastErrorCategory.</summary>
    public string? LastErrorCategory { get; set; }

    /// <summary>Gets or sets LastErrorMessage.</summary>
    public string? LastErrorMessage { get; set; }

    /// <summary>Gets or sets UpdatedAt.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
