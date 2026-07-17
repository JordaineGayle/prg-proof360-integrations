namespace PRG.Proof360.Integrations.Application.Health;

/// <summary>
/// Sanitized connector health response. Never contains secrets, PII, or raw provider bodies.
/// </summary>
public sealed record ConnectorHealthSnapshot(
    string ProviderName,
    string ProviderInstanceId,
    string Status,
    string CircuitState,
    DateTimeOffset? LastSuccessfulProviderCallAt,
    DateTimeOffset? LastSuccessfulSyncAt,
    string? LastFailureCategory,
    string? LastFailureCode,
    DateTimeOffset? LastFailureAt,
    int InboxBacklogCount,
    int OutboxBacklogCount,
    double? OldestBacklogAgeSeconds,
    int DeadLetterCount,
    int UnresolvedDependencyCount,
    int RecentRateLimitCount,
    DateTimeOffset GeneratedAtUtc);
