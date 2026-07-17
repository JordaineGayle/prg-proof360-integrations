using PRG.Proof360.Integrations.Core.Integration;

namespace PRG.Proof360.Integrations.Application.Abstractions.Persistence;

/// <summary>
/// Use-case oriented persistence for connector infrastructure records.
/// </summary>
public interface IIntegrationStore
{
    /// <summary>Stages an identity-link insert.</summary>
    Task AddIdentityLinkAsync(ProviderIdentityLink link, CancellationToken cancellationToken = default);

    /// <summary>Stages an inbox message insert.</summary>
    Task AddInboxMessageAsync(InboxMessage message, CancellationToken cancellationToken = default);

    /// <summary>Stages an outbox message insert.</summary>
    Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default);

    /// <summary>Stages an audit event insert.</summary>
    Task AddAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);

    /// <summary>Inserts or updates connector health/checkpoint state.</summary>
    Task UpsertConnectorStateAsync(ConnectorState state, CancellationToken cancellationToken = default);
}
