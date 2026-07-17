using System.ComponentModel.DataAnnotations;

namespace PRG.Proof360.Integrations.Application.Health;

/// <summary>Thresholds for connector health status projection.</summary>
public sealed class ConnectorHealthOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "ConnectorHealth";

    /// <summary>Inbox backlog at or above this value is Degraded.</summary>
    [Range(1, 1_000_000)]
    public int DegradedInboxBacklogThreshold { get; set; } = 50;

    /// <summary>Outbox backlog at or above this value is Degraded.</summary>
    [Range(1, 1_000_000)]
    public int DegradedOutboxBacklogThreshold { get; set; } = 50;

    /// <summary>Recent rate-limit count at or above this value is Degraded.</summary>
    [Range(1, 1_000_000)]
    public int DegradedRateLimitThreshold { get; set; } = 3;

    /// <summary>
    /// When the last successful provider call is older than this (and no NeedsAttention),
    /// status becomes Offline.
    /// </summary>
    [Range(1, 86_400)]
    public int OfflineNoSuccessSeconds { get; set; } = 300;

    /// <summary>Window used for recent rate-limit trend counts.</summary>
    [Range(1, 86_400)]
    public int RateLimitTrendWindowSeconds { get; set; } = 900;
}
