namespace PRG.Proof360.Integrations.Core.Results;

/// <summary>
/// Success payload for operations with no value. Prefer over null.
/// </summary>
public sealed record Unit
{
    private Unit()
    {
    }

    /// <summary>Singleton unit value.</summary>
    public static Unit Instance { get; } = new();
}
