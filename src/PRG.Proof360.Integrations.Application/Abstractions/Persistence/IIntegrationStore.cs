using PRG.Proof360.Integrations.Application.Health;
using PRG.Proof360.Integrations.Core.Integration;

namespace PRG.Proof360.Integrations.Application.Abstractions.Persistence;

/// <summary>
/// Write/read port for integration sidecar records (identity, inbox, outbox, audit, connector state).
/// </summary>
public interface IIntegrationStore
{
    /// <summary>Stages a new identity link.</summary>
    Task AddIdentityLinkAsync(ProviderIdentityLink link, CancellationToken cancellationToken = default);

    /// <summary>Stages a new inbox message.</summary>
    Task AddInboxMessageAsync(InboxMessage message, CancellationToken cancellationToken = default);

    /// <summary>Stages a new outbox message.</summary>
    Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default);

    /// <summary>Stages a new audit event.</summary>
    Task AddAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);

    /// <summary>Upserts durable connector state/checkpoint.</summary>
    Task UpsertConnectorStateAsync(ConnectorState state, CancellationToken cancellationToken = default);

    /// <summary>Finds durable connector state for a provider instance.</summary>
    Task<ConnectorState?> FindConnectorStateAsync(
        string providerName,
        string providerInstanceId,
        CancellationToken cancellationToken = default);

    /// <summary>Loads inbox/outbox backlog metrics for connector health.</summary>
    Task<IntegrationBacklogMetrics> GetBacklogMetricsAsync(
        string providerInstanceId,
        CancellationToken cancellationToken = default);

    /// <summary>Finds an identity link by external id.</summary>
    Task<ProviderIdentityLink?> FindIdentityByExternalAsync(
        string providerInstanceId,
        string externalEntityType,
        string externalId,
        CancellationToken cancellationToken = default);

    /// <summary>Finds an identity link by canonical entity id.</summary>
    Task<ProviderIdentityLink?> FindIdentityByCanonicalAsync(
        string providerInstanceId,
        string canonicalEntityType,
        Guid canonicalId,
        CancellationToken cancellationToken = default);

    /// <summary>Finds an inbox message by event id.</summary>
    Task<InboxMessage?> FindInboxByEventIdAsync(
        string providerInstanceId,
        string eventId,
        CancellationToken cancellationToken = default);

    /// <summary>Loads an inbox message by primary key.</summary>
    Task<InboxMessage?> FindInboxByIdAsync(Guid inboxMessageId, CancellationToken cancellationToken = default);

    /// <summary>Finds an outbox message by idempotency key.</summary>
    Task<OutboxMessage?> FindOutboxByIdempotencyKeyAsync(
        string providerInstanceId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>Loads an outbox message by primary key.</summary>
    Task<OutboxMessage?> FindOutboxByIdAsync(Guid outboxMessageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claims the next eligible inbox message (Pending/WaitingForDependency, due now).
    /// Uses the concurrency token; returns null when nothing is claimable.
    /// </summary>
    Task<InboxMessage?> ClaimNextInboxMessageAsync(
        string providerInstanceId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claims the next eligible outbox message (Pending due now).
    /// </summary>
    Task<OutboxMessage?> ClaimNextOutboxMessageAsync(
        string providerInstanceId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>Counts identity links for an external entity type.</summary>
    Task<int> CountIdentityLinksAsync(
        string providerInstanceId,
        string externalEntityType,
        CancellationToken cancellationToken = default);

    /// <summary>Counts outbox messages for an operation (tests).</summary>
    Task<int> CountOutboxMessagesAsync(
        string providerInstanceId,
        string operationType,
        CancellationToken cancellationToken = default);

    /// <summary>Counts audit events for an operation/result (tests).</summary>
    Task<int> CountAuditEventsAsync(
        string operation,
        string? result = null,
        CancellationToken cancellationToken = default);

    /// <summary>Counts inbox messages in a state for a provider instance.</summary>
    Task<int> CountInboxByStateAsync(
        string providerInstanceId,
        string state,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Demo helper: makes WaitingForDependency messages due immediately so the next process batch can claim them.
    /// </summary>
    Task<int> MakeWaitingDependenciesDueAsync(
        string providerInstanceId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Demo helper: bumps waiting-dependency messages to the final attempt budget so the next process can dead-letter them.
    /// </summary>
    Task<int> PrepareWaitingDependenciesForExhaustionAsync(
        string providerInstanceId,
        int maxAttempts,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);
}
