using Microsoft.EntityFrameworkCore;
using PRG.Proof360.Integrations.Application.Abstractions.Persistence;
using PRG.Proof360.Integrations.Application.Health;
using PRG.Proof360.Integrations.Core.Integration;

namespace PRG.Proof360.Integrations.Infrastructure.Persistence;

/// <summary>
/// EF-backed integration store with inbox claim semantics.
/// </summary>
internal sealed class IntegrationStore : IIntegrationStore
{
    private readonly ConnectorDbContext _dbContext;

    public IntegrationStore(ConnectorDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task AddIdentityLinkAsync(ProviderIdentityLink link, CancellationToken cancellationToken = default)
        => _dbContext.ProviderIdentityLinks.AddAsync(link, cancellationToken).AsTask();

    /// <inheritdoc />
    public Task AddInboxMessageAsync(InboxMessage message, CancellationToken cancellationToken = default)
        => _dbContext.InboxMessages.AddAsync(message, cancellationToken).AsTask();

    /// <inheritdoc />
    public Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        => _dbContext.OutboxMessages.AddAsync(message, cancellationToken).AsTask();

    /// <inheritdoc />
    public Task AddAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        => _dbContext.AuditEvents.AddAsync(auditEvent, cancellationToken).AsTask();

    /// <inheritdoc />
    public async Task UpsertConnectorStateAsync(ConnectorState state, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.ConnectorStates
            .SingleOrDefaultAsync(
                x => x.ProviderName == state.ProviderName && x.ProviderInstanceId == state.ProviderInstanceId,
                cancellationToken);

        if (existing is null)
        {
            await _dbContext.ConnectorStates.AddAsync(state, cancellationToken);
            return;
        }

        existing.LastSuccessfulSyncAt = state.LastSuccessfulSyncAt;
        existing.LastCheckpoint = state.LastCheckpoint;
        existing.CircuitState = state.CircuitState;
        existing.InboxBacklogCount = state.InboxBacklogCount;
        existing.OutboxBacklogCount = state.OutboxBacklogCount;
        existing.DeadLetterCount = state.DeadLetterCount;
        existing.LastErrorCategory = state.LastErrorCategory;
        existing.LastErrorMessage = state.LastErrorMessage;
        existing.UpdatedAt = state.UpdatedAt;
    }

    /// <inheritdoc />
    public Task<ConnectorState?> FindConnectorStateAsync(
        string providerName,
        string providerInstanceId,
        CancellationToken cancellationToken = default) =>
        _dbContext.ConnectorStates.SingleOrDefaultAsync(
            x => x.ProviderName == providerName && x.ProviderInstanceId == providerInstanceId,
            cancellationToken);

    /// <inheritdoc />
    public async Task<IntegrationBacklogMetrics> GetBacklogMetricsAsync(
        string providerInstanceId,
        CancellationToken cancellationToken = default)
    {
        var inboxBacklogStates = new[]
        {
            InboxMessageStates.Pending,
            InboxMessageStates.Processing,
            InboxMessageStates.WaitingForDependency
        };
        var outboxBacklogStates = new[]
        {
            OutboxMessageStates.Pending,
            OutboxMessageStates.Processing
        };

        var inbox = await _dbContext.InboxMessages
            .Where(x => x.ProviderInstanceId == providerInstanceId && inboxBacklogStates.Contains(x.State))
            .Select(x => new { x.ReceivedAt, x.State })
            .ToListAsync(cancellationToken);

        var outbox = await _dbContext.OutboxMessages
            .Where(x => x.ProviderInstanceId == providerInstanceId && outboxBacklogStates.Contains(x.State))
            .Select(x => new { x.CreatedAt })
            .ToListAsync(cancellationToken);

        var deadLetterCount =
            await _dbContext.InboxMessages.CountAsync(
                x => x.ProviderInstanceId == providerInstanceId && x.State == InboxMessageStates.DeadLettered,
                cancellationToken)
            + await _dbContext.OutboxMessages.CountAsync(
                x => x.ProviderInstanceId == providerInstanceId && x.State == OutboxMessageStates.DeadLettered,
                cancellationToken);

        var unresolved = inbox.Count(x => x.State == InboxMessageStates.WaitingForDependency);
        DateTimeOffset? oldest = null;
        if (inbox.Count > 0)
        {
            oldest = inbox.Min(x => x.ReceivedAt);
        }

        if (outbox.Count > 0)
        {
            var outboxOldest = outbox.Min(x => x.CreatedAt);
            oldest = oldest is null || outboxOldest < oldest ? outboxOldest : oldest;
        }

        return new IntegrationBacklogMetrics(
            inbox.Count,
            outbox.Count,
            deadLetterCount,
            unresolved,
            oldest);
    }

    /// <inheritdoc />
    public Task<ProviderIdentityLink?> FindIdentityByExternalAsync(
        string providerInstanceId,
        string externalEntityType,
        string externalId,
        CancellationToken cancellationToken = default) =>
        _dbContext.ProviderIdentityLinks.SingleOrDefaultAsync(
            x => x.ProviderInstanceId == providerInstanceId &&
                 x.ExternalEntityType == externalEntityType &&
                 x.ExternalId == externalId,
            cancellationToken);

    /// <inheritdoc />
    public Task<ProviderIdentityLink?> FindIdentityByCanonicalAsync(
        string providerInstanceId,
        string canonicalEntityType,
        Guid canonicalId,
        CancellationToken cancellationToken = default) =>
        _dbContext.ProviderIdentityLinks.SingleOrDefaultAsync(
            x => x.ProviderInstanceId == providerInstanceId &&
                 x.CanonicalEntityType == canonicalEntityType &&
                 x.CanonicalId == canonicalId,
            cancellationToken);

    /// <inheritdoc />
    public Task<InboxMessage?> FindInboxByEventIdAsync(
        string providerInstanceId,
        string eventId,
        CancellationToken cancellationToken = default) =>
        _dbContext.InboxMessages.SingleOrDefaultAsync(
            x => x.ProviderInstanceId == providerInstanceId && x.EventId == eventId,
            cancellationToken);

    /// <inheritdoc />
    public Task<InboxMessage?> FindInboxByIdAsync(Guid inboxMessageId, CancellationToken cancellationToken = default) =>
        _dbContext.InboxMessages.SingleOrDefaultAsync(x => x.Id == inboxMessageId, cancellationToken);

    /// <inheritdoc />
    public Task<OutboxMessage?> FindOutboxByIdempotencyKeyAsync(
        string providerInstanceId,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        _dbContext.OutboxMessages.SingleOrDefaultAsync(
            x => x.ProviderInstanceId == providerInstanceId && x.IdempotencyKey == idempotencyKey,
            cancellationToken);

    /// <inheritdoc />
    public Task<OutboxMessage?> FindOutboxByIdAsync(Guid outboxMessageId, CancellationToken cancellationToken = default) =>
        _dbContext.OutboxMessages.SingleOrDefaultAsync(x => x.Id == outboxMessageId, cancellationToken);

    /// <inheritdoc />
    public async Task<InboxMessage?> ClaimNextInboxMessageAsync(
        string providerInstanceId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        // Bounded claim attempts under concurrency token races.
        // SQLite EF cannot translate nullable DateTimeOffset comparisons; filter due times after a bounded fetch.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            // SQLite cannot ORDER BY / compare DateTimeOffset in SQL; materialize then order/filter in memory.
            var batch = await _dbContext.InboxMessages
                .Where(x => x.ProviderInstanceId == providerInstanceId)
                .Where(x => x.State == InboxMessageStates.Pending || x.State == InboxMessageStates.WaitingForDependency)
                .Take(50)
                .ToListAsync(cancellationToken);

            var candidate = batch
                .Where(x => x.NextAttemptAt is null || x.NextAttemptAt <= utcNow)
                .OrderBy(x => x.ReceivedAt)
                .FirstOrDefault();
            if (candidate is null)
            {
                return null;
            }

            candidate.State = InboxMessageStates.Processing;
            candidate.AttemptCount += 1;
            candidate.ErrorCategory = null;
            candidate.ErrorMessage = null;
            candidate.RowVersion += 1;

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                return candidate;
            }
            catch (DbUpdateConcurrencyException)
            {
                foreach (var entry in _dbContext.ChangeTracker.Entries<InboxMessage>())
                {
                    await entry.ReloadAsync(cancellationToken);
                }
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<OutboxMessage?> ClaimNextOutboxMessageAsync(
        string providerInstanceId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        // SQLite cannot ORDER BY / compare DateTimeOffset in SQL; materialize then filter/order in memory.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var batch = await _dbContext.OutboxMessages
                .Where(x => x.ProviderInstanceId == providerInstanceId)
                .Where(x => x.State == OutboxMessageStates.Pending)
                .Take(50)
                .ToListAsync(cancellationToken);

            var candidate = batch
                .Where(x => x.NextAttemptAt is null || x.NextAttemptAt <= utcNow)
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefault();

            if (candidate is null)
            {
                return null;
            }

            candidate.State = OutboxMessageStates.Processing;
            candidate.AttemptCount += 1;
            candidate.ErrorCategory = null;
            candidate.ErrorMessage = null;
            candidate.RowVersion += 1;

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                return candidate;
            }
            catch (DbUpdateConcurrencyException)
            {
                foreach (var entry in _dbContext.ChangeTracker.Entries<OutboxMessage>())
                {
                    await entry.ReloadAsync(cancellationToken);
                }
            }
        }

        return null;
    }

    /// <inheritdoc />
    public Task<int> CountIdentityLinksAsync(
        string providerInstanceId,
        string externalEntityType,
        CancellationToken cancellationToken = default) =>
        _dbContext.ProviderIdentityLinks.CountAsync(
            x => x.ProviderInstanceId == providerInstanceId && x.ExternalEntityType == externalEntityType,
            cancellationToken);

    /// <inheritdoc />
    public Task<int> CountOutboxMessagesAsync(
        string providerInstanceId,
        string operationType,
        CancellationToken cancellationToken = default) =>
        _dbContext.OutboxMessages.CountAsync(
            x => x.ProviderInstanceId == providerInstanceId && x.OperationType == operationType,
            cancellationToken);

    /// <inheritdoc />
    public Task<int> CountAuditEventsAsync(
        string operation,
        string? result = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.AuditEvents.Where(x => x.Operation == operation);
        if (result is not null)
        {
            query = query.Where(x => x.Result == result);
        }

        return query.CountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> CountInboxByStateAsync(
        string providerInstanceId,
        string state,
        CancellationToken cancellationToken = default) =>
        _dbContext.InboxMessages.CountAsync(
            x => x.ProviderInstanceId == providerInstanceId && x.State == state,
            cancellationToken);

    /// <inheritdoc />
    public async Task<int> MakeWaitingDependenciesDueAsync(
        string providerInstanceId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        var waiting = await _dbContext.InboxMessages
            .Where(x => x.ProviderInstanceId == providerInstanceId)
            .Where(x => x.State == InboxMessageStates.WaitingForDependency)
            .ToListAsync(cancellationToken);

        foreach (var message in waiting)
        {
            message.NextAttemptAt = utcNow.AddSeconds(-1);
            message.RowVersion += 1;
        }

        if (waiting.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return waiting.Count;
    }
}
