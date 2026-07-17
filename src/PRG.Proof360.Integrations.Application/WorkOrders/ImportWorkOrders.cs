using System.Text.Json;
using Microsoft.Extensions.Options;
using PRG.Proof360.Integrations.Application.Contractors;
using PRG.Proof360.Integrations.Application.Errors;
using PRG.Proof360.Integrations.Application.Inbox;
using PRG.Proof360.Integrations.Application.Outcomes;
using PRG.Proof360.Integrations.Core.Integration;
using PRG.Proof360.Integrations.Core.Providers.Capabilities;
using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Core.Results;

namespace PRG.Proof360.Integrations.Application.WorkOrders;

/// <summary>
/// Typed success for work-order import orchestration.
/// </summary>
public abstract record ImportWorkOrdersOutcome
{
    private ImportWorkOrdersOutcome()
    {
    }

    /// <summary>Import pass completed.</summary>
    public sealed record Completed(int CreatedCount, int UpdatedCount, int WaitingCount) : ImportWorkOrdersOutcome;
}

/// <summary>
/// Imports work-order snapshots through durable inbox + shared process path.
/// </summary>
public sealed class ImportWorkOrdersHandler
{
    private readonly IWorkOrderSnapshotSource _source;
    private readonly IProviderCapabilities _capabilities;
    private readonly ReceiveProviderEventHandler _receive;
    private readonly ProcessInboxMessageHandler _process;
    private readonly InboundSyncOptions _options;

    /// <summary>Creates the handler.</summary>
    public ImportWorkOrdersHandler(
        IWorkOrderSnapshotSource source,
        IProviderCapabilities capabilities,
        ReceiveProviderEventHandler receive,
        ProcessInboxMessageHandler process,
        IOptions<InboundSyncOptions> options)
    {
        _source = source;
        _capabilities = capabilities;
        _receive = receive;
        _process = process;
        _options = options.Value;
    }

    /// <summary>
    /// Pulls work orders (HTTP outside TX), receives into inbox, processes.
    /// </summary>
    public async Task<Result<ImportWorkOrdersOutcome, IntegrationFailure>> HandleAsync(
        CancellationToken cancellationToken)
    {
        if (!_capabilities.Supports(ProviderCapability.WorkOrderSnapshots))
        {
            return Result<ImportWorkOrdersOutcome, IntegrationFailure>.Fail(
                new IntegrationFailure(
                    FailureCodes.UnsupportedCapability,
                    "Provider does not support work-order snapshots.",
                    FailureCategory.ProviderContract));
        }

        var list = await _source.ListAsync(cancellationToken);
        if (list.IsFailure)
        {
            return Result<ImportWorkOrdersOutcome, IntegrationFailure>.Fail(
                ProviderFailureTranslator.ToIntegrationFailure(
                    ((Result<IReadOnlyList<WorkOrderSnapshot>, ProviderFailure>.Failed)list).Error));
        }

        var snapshots = ((Result<IReadOnlyList<WorkOrderSnapshot>, ProviderFailure>.Succeeded)list).Value;
        foreach (var snapshot in snapshots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var envelope = JsonSerializer.Serialize(snapshot);
            var receive = await _receive.HandleAsync(
                new ReceiveProviderEventCommand
                {
                    ProviderName = snapshot.ProviderName,
                    ProviderInstanceId = snapshot.ProviderInstanceId,
                    EventId = SyntheticEventIds.ForWorkOrder(snapshot),
                    EventType = InboxEventTypes.WorkOrderSnapshot,
                    PayloadEnvelope = envelope,
                    PayloadHash = SyntheticEventIds.HashPayload(snapshot),
                    EventVersion = snapshot.EntityVersion,
                    SchemaVersion = snapshot.SchemaVersion,
                    OccurredAt = snapshot.OccurredAt ?? DateTimeOffset.UtcNow
                },
                cancellationToken);

            if (receive.IsFailure)
            {
                return Result<ImportWorkOrdersOutcome, IntegrationFailure>.Fail(
                    ((Result<ReceiveEventOutcome, IntegrationFailure>.Failed)receive).Error);
            }
        }

        var created = 0;
        var updated = 0;
        var waiting = 0;
        for (var i = 0; i < _options.MaxProcessBatch; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var processed = await _process.HandleAsync(_capabilities.ProviderInstanceId, cancellationToken);
            if (processed.IsFailure)
            {
                continue;
            }

            switch (((Result<ProcessInboxOutcome, IntegrationFailure>.Succeeded)processed).Value)
            {
                case ProcessInboxOutcome.Idle:
                    return Result<ImportWorkOrdersOutcome, IntegrationFailure>.Ok(
                        new ImportWorkOrdersOutcome.Completed(created, updated, waiting));
                case ProcessInboxOutcome.WorkOrderApplied applied:
                    switch (applied.Outcome)
                    {
                        case ApplyWorkOrderOutcome.Created:
                            created++;
                            break;
                        case ApplyWorkOrderOutcome.Updated:
                            updated++;
                            break;
                    }

                    break;
                case ProcessInboxOutcome.WaitingForDependency:
                    waiting++;
                    break;
            }
        }

        return Result<ImportWorkOrdersOutcome, IntegrationFailure>.Ok(
            new ImportWorkOrdersOutcome.Completed(created, updated, waiting));
    }
}
