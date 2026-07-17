using PRG.Proof360.Integrations.Application.Abstractions.Time;

namespace PRG.Proof360.Integrations.UnitTests.Mapping;

internal sealed class FixedClock : IClock
{
    public FixedClock(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
        UtcToday = DateOnly.FromDateTime(utcNow.UtcDateTime);
    }

    public DateTimeOffset UtcNow { get; }

    public DateOnly UtcToday { get; }
}
