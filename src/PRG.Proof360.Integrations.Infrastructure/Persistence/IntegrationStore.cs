using Microsoft.EntityFrameworkCore;
using PRG.Proof360.Integrations.Application.Abstractions.Persistence;
using PRG.Proof360.Integrations.Core.Integration;

namespace PRG.Proof360.Integrations.Infrastructure.Persistence;

/// <summary>
/// EF-backed integration store.
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
}
