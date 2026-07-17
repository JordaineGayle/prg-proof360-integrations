namespace PRG.Proof360.Integrations.Application.Health;

/// <summary>User-visible connector health statuses.</summary>
public static class ConnectorHealthStatuses
{
    /// <summary>Provider calls succeeding; backlogs within thresholds.</summary>
    public const string Healthy = "Healthy";

    /// <summary>Elevated backlog, rate limits, or half-open circuit.</summary>
    public const string Degraded = "Degraded";

    /// <summary>Circuit open or prolonged provider silence.</summary>
    public const string Offline = "Offline";

    /// <summary>Authentication/configuration requires operator action.</summary>
    public const string NeedsAttention = "NeedsAttention";
}
