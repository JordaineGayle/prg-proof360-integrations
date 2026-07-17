using System.Text.Json;
using Microsoft.Extensions.Options;
using PRG.Proof360.Integrations.Application.Errors;
using PRG.Proof360.Integrations.Application.Inbox;
using PRG.Proof360.Integrations.Application.Outcomes;
using PRG.Proof360.Integrations.Core.Integration;
using PRG.Proof360.Integrations.Core.Providers.Capabilities;
using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Core.Results;

namespace PRG.Proof360.Integrations.Application.Contractors;

/// <summary>
/// Options for inbound polling orchestration.
/// </summary>
public sealed class InboundSyncOptions
{
    /// <summary>Section name.</summary>
    public const string SectionName = "InboundSync";

    /// <summary>Enable background polling worker.</summary>
    public bool PollingEnabled { get; set; }

    /// <summary>Polling interval in seconds.</summary>
    public int PollingIntervalSeconds { get; set; } = 60;

    /// <summary>Max inbox messages to process per sync cycle.</summary>
    public int MaxProcessBatch { get; set; } = 100;
}

/// <summary>
/// Imports contractor snapshots: provider HTTP outside DB TX, then durable inbox + shared process path.
/// </summary>
public sealed class ImportContractorsHandler
{
    private readonly IContractorSnapshotSource _source;
    private readonly IProviderCapabilities _capabilities;
    private readonly ReceiveProviderEventHandler _receive;
    private readonly ProcessInboxMessageHandler _process;
    private readonly InboundSyncOptions _options;

    /// <summary>Creates the handler.</summary>
    public ImportContractorsHandler(
        IContractorSnapshotSource source,
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
    /// Pulls contractors from the provider, receives each snapshot into the inbox, then processes.
    /// </summary>
    public async Task<Result<ImportContractorsOutcome, IntegrationFailure>> HandleAsync(
        CancellationToken cancellationToken)
    {
        if (!_capabilities.Supports(ProviderCapability.ContractorSnapshots))
        {
            return Result<ImportContractorsOutcome, IntegrationFailure>.Fail(
                new IntegrationFailure(
                    FailureCodes.UnsupportedCapability,
                    "Provider does not support contractor snapshots.",
                    FailureCategory.ProviderContract));
        }

        // HTTP outside any database transaction.
        var list = await _source.ListAsync(cancellationToken);
        if (list.IsFailure)
        {
            return Result<ImportContractorsOutcome, IntegrationFailure>.Fail(
                ProviderFailureTranslator.ToIntegrationFailure(
                    ((Result<IReadOnlyList<ContractorSnapshot>, ProviderFailure>.Failed)list).Error));
        }

        var snapshots = ((Result<IReadOnlyList<ContractorSnapshot>, ProviderFailure>.Succeeded)list).Value;
        foreach (var snapshot in snapshots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var envelope = JsonSerializer.Serialize(snapshot);
            var receive = await _receive.HandleAsync(
                new ReceiveProviderEventCommand
                {
                    ProviderName = snapshot.ProviderName,
                    ProviderInstanceId = snapshot.ProviderInstanceId,
                    EventId = SyntheticEventIds.ForContractor(snapshot),
                    EventType = InboxEventTypes.ContractorSnapshot,
                    PayloadEnvelope = envelope,
                    PayloadHash = SyntheticEventIds.HashPayload(snapshot),
                    EventVersion = snapshot.EntityVersion,
                    SchemaVersion = snapshot.SchemaVersion,
                    OccurredAt = DateTimeOffset.UtcNow
                },
                cancellationToken);

            if (receive.IsFailure)
            {
                return Result<ImportContractorsOutcome, IntegrationFailure>.Fail(
                    ((Result<ReceiveEventOutcome, IntegrationFailure>.Failed)receive).Error);
            }
        }

        var created = 0;
        var updated = 0;
        for (var i = 0; i < _options.MaxProcessBatch; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var processed = await _process.HandleAsync(_capabilities.ProviderInstanceId, cancellationToken);
            if (processed.IsFailure)
            {
                // Retryable process failure — continue batch when possible.
                continue;
            }

            switch (((Result<ProcessInboxOutcome, IntegrationFailure>.Succeeded)processed).Value)
            {
                case ProcessInboxOutcome.Idle:
                    return Result<ImportContractorsOutcome, IntegrationFailure>.Ok(
                        new ImportContractorsOutcome.Completed(created, updated));
                case ProcessInboxOutcome.ContractorApplied applied:
                    switch (applied.Outcome)
                    {
                        case ApplyContractorOutcome.Created:
                            created++;
                            break;
                        case ApplyContractorOutcome.Updated:
                        case ApplyContractorOutcome.Restricted:
                            updated++;
                            break;
                    }

                    break;
                case ProcessInboxOutcome.WorkOrderApplied:
                    // Unexpected during contractor-first import; keep draining.
                    break;
            }
        }

        return Result<ImportContractorsOutcome, IntegrationFailure>.Ok(
            new ImportContractorsOutcome.Completed(created, updated));
    }
}
