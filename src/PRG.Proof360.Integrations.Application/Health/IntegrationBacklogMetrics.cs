namespace PRG.Proof360.Integrations.Application.Health;

/// <summary>Durable inbox/outbox backlog metrics for health projection.</summary>
public sealed record IntegrationBacklogMetrics(
    int InboxBacklogCount,
    int OutboxBacklogCount,
    int DeadLetterCount,
    int UnresolvedDependencyCount,
    DateTimeOffset? OldestBacklogCreatedAt);
