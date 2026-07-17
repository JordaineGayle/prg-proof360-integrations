using PRG.Proof360.Integrations.Application.Abstractions.Persistence;
using PRG.Proof360.Integrations.Application.Abstractions.Time;
using PRG.Proof360.Integrations.Core.Providers;
using PRG.Proof360.Integrations.Core.Providers.Health;

namespace PRG.Proof360.Integrations.Application.Health;

/// <summary>
/// Builds the sanitized FieldFlow connector health projection.
/// </summary>
public sealed class GetConnectorHealthHandler
{
    private readonly IConnectorRuntimeHealthSource _runtime;
    private readonly IIntegrationStore _store;
    private readonly IConnectorUnitOfWork _unitOfWork;
    private readonly ConnectorHealthStatusPolicy _policy;
    private readonly IClock _clock;

    /// <summary>Creates the handler.</summary>
    public GetConnectorHealthHandler(
        IConnectorRuntimeHealthSource runtime,
        IIntegrationStore store,
        IConnectorUnitOfWork unitOfWork,
        ConnectorHealthStatusPolicy policy,
        IClock clock)
    {
        _runtime = runtime;
        _store = store;
        _unitOfWork = unitOfWork;
        _policy = policy;
        _clock = clock;
    }

    /// <summary>Evaluates current connector health.</summary>
    public async Task<ConnectorHealthSnapshot> HandleAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = _clock.UtcNow;
        var backlog = await _store.GetBacklogMetricsAsync(_runtime.ProviderInstanceId, cancellationToken);
        var state = await _store.FindConnectorStateAsync(
            ProviderNames.FieldFlow,
            _runtime.ProviderInstanceId,
            cancellationToken);

        var snapshot = _policy.Evaluate(_runtime, backlog, state?.LastSuccessfulSyncAt, utcNow);

        // Persist a durable health-facing projection (sanitized only).
        await _store.UpsertConnectorStateAsync(
            new Core.Integration.ConnectorState
            {
                Id = state?.Id ?? Guid.NewGuid(),
                ProviderName = ProviderNames.FieldFlow,
                ProviderInstanceId = _runtime.ProviderInstanceId,
                LastSuccessfulSyncAt = state?.LastSuccessfulSyncAt,
                LastCheckpoint = state?.LastCheckpoint,
                CircuitState = snapshot.CircuitState,
                InboxBacklogCount = snapshot.InboxBacklogCount,
                OutboxBacklogCount = snapshot.OutboxBacklogCount,
                DeadLetterCount = snapshot.DeadLetterCount,
                LastErrorCategory = snapshot.LastFailureCategory,
                LastErrorMessage = snapshot.LastFailureCode,
                UpdatedAt = utcNow
            },
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return snapshot;
    }
}
