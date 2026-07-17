namespace PRG.Proof360.Integrations.Application.Abstractions.Time;

/// <summary>
/// Default UTC clock. Infrastructure may replace this registration later.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public DateOnly UtcToday => DateOnly.FromDateTime(DateTime.UtcNow);
}
