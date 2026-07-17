namespace PRG.Proof360.Integrations.Core.Integration;

/// <summary>
/// Explicit inbox processing states. Dead-letter is a state, not a canonical entity.
/// </summary>
public static class InboxMessageStates
{
    /// <summary>Pending state value.</summary>
    public const string Pending = "Pending";
    /// <summary>Processing state value.</summary>
    public const string Processing = "Processing";
    /// <summary>WaitingForDependency state value.</summary>
    public const string WaitingForDependency = "WaitingForDependency";
    /// <summary>Completed state value.</summary>
    public const string Completed = "Completed";
    /// <summary>DeadLettered state value.</summary>
    public const string DeadLettered = "DeadLettered";
}
