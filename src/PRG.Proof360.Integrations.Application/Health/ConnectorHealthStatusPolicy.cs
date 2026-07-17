using Microsoft.Extensions.Options;
using PRG.Proof360.Integrations.Core.Providers.Health;

namespace PRG.Proof360.Integrations.Application.Health;

/// <summary>
/// Deterministic user-visible connector health status rules (Application policy).
/// </summary>
public sealed class ConnectorHealthStatusPolicy
{
    private readonly ConnectorHealthOptions _options;

    /// <summary>Creates the policy.</summary>
    public ConnectorHealthStatusPolicy(IOptions<ConnectorHealthOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Evaluates sanitized health from runtime signals and durable backlog metrics.
    /// </summary>
    public ConnectorHealthSnapshot Evaluate(
        IConnectorRuntimeHealthSource runtime,
        IntegrationBacklogMetrics backlog,
        DateTimeOffset? lastSuccessfulSyncAt,
        DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(backlog);

        var rateLimitCount = runtime.CountRecentRateLimits(
            utcNow,
            TimeSpan.FromSeconds(_options.RateLimitTrendWindowSeconds));

        var oldestAgeSeconds = backlog.OldestBacklogCreatedAt is { } oldest
            ? Math.Max(0, (utcNow - oldest).TotalSeconds)
            : (double?)null;

        var status = ResolveStatus(runtime, backlog, rateLimitCount, utcNow);

        return new ConnectorHealthSnapshot(
            runtime.ProviderName,
            runtime.ProviderInstanceId,
            status,
            runtime.CircuitState,
            runtime.LastSuccessfulProviderCallAt,
            lastSuccessfulSyncAt,
            runtime.LastFailureCategory,
            runtime.LastFailureCode,
            runtime.LastFailureAt,
            backlog.InboxBacklogCount,
            backlog.OutboxBacklogCount,
            oldestAgeSeconds,
            backlog.DeadLetterCount,
            backlog.UnresolvedDependencyCount,
            rateLimitCount,
            utcNow);
    }

    /// <summary>
    /// Status precedence: NeedsAttention → Offline → Degraded → Healthy.
    /// </summary>
    public string ResolveStatus(
        IConnectorRuntimeHealthSource runtime,
        IntegrationBacklogMetrics backlog,
        int recentRateLimitCount,
        DateTimeOffset utcNow)
    {
        if (runtime.NeedsAttention ||
            string.Equals(runtime.LastFailureCategory, "Authentication", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(runtime.LastFailureCategory, "Forbidden", StringComparison.OrdinalIgnoreCase))
        {
            return ConnectorHealthStatuses.NeedsAttention;
        }

        if (string.Equals(runtime.CircuitState, "Open", StringComparison.OrdinalIgnoreCase))
        {
            return ConnectorHealthStatuses.Offline;
        }

        if (runtime.LastSuccessfulProviderCallAt is { } lastSuccess)
        {
            var silence = utcNow - lastSuccess;
            if (silence >= TimeSpan.FromSeconds(_options.OfflineNoSuccessSeconds) &&
                runtime.LastFailureAt is not null &&
                runtime.LastFailureAt >= lastSuccess)
            {
                return ConnectorHealthStatuses.Offline;
            }
        }
        else if (runtime.LastFailureAt is not null)
        {
            // Never succeeded in this process and already failing.
            return ConnectorHealthStatuses.Offline;
        }

        if (string.Equals(runtime.CircuitState, "HalfOpen", StringComparison.OrdinalIgnoreCase) ||
            backlog.InboxBacklogCount >= _options.DegradedInboxBacklogThreshold ||
            backlog.OutboxBacklogCount >= _options.DegradedOutboxBacklogThreshold ||
            backlog.DeadLetterCount > 0 ||
            recentRateLimitCount >= _options.DegradedRateLimitThreshold)
        {
            return ConnectorHealthStatuses.Degraded;
        }

        return ConnectorHealthStatuses.Healthy;
    }
}
