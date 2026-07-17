namespace PRG.Proof360.Integrations.Core.Integration;

/// <summary>
/// Explicit outbox processing states.
/// </summary>
public static class OutboxMessageStates
{
    /// <summary>Pending state value.</summary>
    public const string Pending = "Pending";
    /// <summary>Processing state value.</summary>
    public const string Processing = "Processing";
    /// <summary>Completed state value.</summary>
    public const string Completed = "Completed";
    /// <summary>DeadLettered state value.</summary>
    public const string DeadLettered = "DeadLettered";
}
