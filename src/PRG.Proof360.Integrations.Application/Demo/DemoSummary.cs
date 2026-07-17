using PRG.Proof360.Integrations.Application.Abstractions.Persistence;
using PRG.Proof360.Integrations.Application.Abstractions.Time;
using PRG.Proof360.Integrations.Application.Errors;
using PRG.Proof360.Integrations.Application.Inbox;
using PRG.Proof360.Integrations.Core.Integration;
using PRG.Proof360.Integrations.Core.Providers.Capabilities;
using PRG.Proof360.Integrations.Core.Results;

namespace PRG.Proof360.Integrations.Application.Demo;

/// <summary>Sanitized local-demo snapshot (no PII/secrets).</summary>
public sealed record DemoSummarySnapshot(
    int VendorCount,
    int JobCount,
    int ContractorLinks,
    int WorkOrderLinks,
    int InboxPending,
    int InboxWaitingForDependency,
    int InboxDeadLettered,
    int InboxCompleted);

/// <summary>Development-only sanitized counts for the five-minute demo.</summary>
public sealed class GetDemoSummaryHandler
{
    private readonly ICanonicalWriter _canonical;
    private readonly IIntegrationStore _store;
    private readonly IProviderCapabilities _capabilities;

    /// <summary>Creates the handler.</summary>
    public GetDemoSummaryHandler(
        ICanonicalWriter canonical,
        IIntegrationStore store,
        IProviderCapabilities capabilities)
    {
        _canonical = canonical;
        _store = store;
        _capabilities = capabilities;
    }

    /// <summary>Loads sanitized demo counts.</summary>
    public async Task<DemoSummarySnapshot> HandleAsync(CancellationToken cancellationToken)
    {
        var instance = _capabilities.ProviderInstanceId;
        return new DemoSummarySnapshot(
            await _canonical.CountVendorsAsync(cancellationToken),
            await _canonical.CountJobsAsync(cancellationToken),
            await _store.CountIdentityLinksAsync(instance, ExternalEntityTypes.Contractor, cancellationToken),
            await _store.CountIdentityLinksAsync(instance, ExternalEntityTypes.WorkOrder, cancellationToken),
            await _store.CountInboxByStateAsync(instance, InboxMessageStates.Pending, cancellationToken),
            await _store.CountInboxByStateAsync(instance, InboxMessageStates.WaitingForDependency, cancellationToken),
            await _store.CountInboxByStateAsync(instance, InboxMessageStates.DeadLettered, cancellationToken),
            await _store.CountInboxByStateAsync(instance, InboxMessageStates.Completed, cancellationToken));
    }
}

/// <summary>Makes waiting dependency inbox messages due and processes a bounded batch.</summary>
public sealed class NudgeWaitingDependenciesHandler
{
    private readonly IIntegrationStore _store;
    private readonly IProviderCapabilities _capabilities;
    private readonly ProcessInboxMessageHandler _process;
    private readonly IClock _clock;

    /// <summary>Creates the handler.</summary>
    public NudgeWaitingDependenciesHandler(
        IIntegrationStore store,
        IProviderCapabilities capabilities,
        ProcessInboxMessageHandler process,
        IClock clock)
    {
        _store = store;
        _capabilities = capabilities;
        _process = process;
        _clock = clock;
    }

    /// <summary>Nudges waiting messages due and processes up to <paramref name="maxBatch"/>.</summary>
    public async Task<(int MadeDue, int Processed)> HandleAsync(int maxBatch, CancellationToken cancellationToken)
    {
        var madeDue = await _store.MakeWaitingDependenciesDueAsync(
            _capabilities.ProviderInstanceId,
            _clock.UtcNow,
            cancellationToken);

        var processed = 0;
        for (var i = 0; i < maxBatch; i++)
        {
            var outcome = await _process.HandleAsync(_capabilities.ProviderInstanceId, cancellationToken);
            if (!outcome.IsSuccess)
            {
                break;
            }

            var value = ((Result<ProcessInboxOutcome, IntegrationFailure>.Succeeded)outcome).Value;
            if (value is ProcessInboxOutcome.Idle)
            {
                break;
            }

            processed++;
        }

        return (madeDue, processed);
    }
}
