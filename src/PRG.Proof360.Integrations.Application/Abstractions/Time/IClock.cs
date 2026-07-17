namespace PRG.Proof360.Integrations.Application.Abstractions.Time;

/// <summary>
/// Injectable clock for Proof360-owned timestamps such as <c>created_at</c>.
/// </summary>
public interface IClock
{
    /// <summary>Current UTC instant.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>Current UTC calendar date.</summary>
    DateOnly UtcToday { get; }
}
