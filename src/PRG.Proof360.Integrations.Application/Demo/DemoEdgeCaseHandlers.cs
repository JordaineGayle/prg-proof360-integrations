using PRG.Proof360.Integrations.Application.Abstractions.Persistence;
using PRG.Proof360.Integrations.Application.Abstractions.Time;
using PRG.Proof360.Integrations.Application.Dispatch;
using PRG.Proof360.Integrations.Application.Errors;
using PRG.Proof360.Integrations.Application.Inbox;
using PRG.Proof360.Integrations.Core.Integration;
using PRG.Proof360.Integrations.Core.Providers;
using PRG.Proof360.Integrations.Core.Providers.Capabilities;
using PRG.Proof360.Integrations.Core.Results;
using PRG.Proof360.Integrations.Application.Mapping;
using PRG.Proof360.Integrations.Domain.Canonical;

namespace PRG.Proof360.Integrations.Application.Demo;

/// <summary>Development helper: exhaust waiting-dependency inbox messages into the dead-letter queue.</summary>
public sealed class ExhaustWaitingDependenciesDemoHandler
{
    private readonly IIntegrationStore _store;
    private readonly IProviderCapabilities _capabilities;
    private readonly ProcessInboxMessageHandler _process;
    private readonly IClock _clock;

    /// <summary>Creates the handler.</summary>
    public ExhaustWaitingDependenciesDemoHandler(
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

    /// <summary>Prepares waiting messages for exhaustion and processes a bounded batch.</summary>
    public async Task<(int Prepared, int Processed, int DeadLettered)> HandleAsync(
        int maxAttempts,
        int maxBatch,
        CancellationToken cancellationToken)
    {
        var prepared = await _store.PrepareWaitingDependenciesForExhaustionAsync(
            _capabilities.ProviderInstanceId,
            maxAttempts,
            _clock.UtcNow,
            cancellationToken);

        var beforeDead = await _store.CountInboxByStateAsync(
            _capabilities.ProviderInstanceId,
            InboxMessageStates.DeadLettered,
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

        var afterDead = await _store.CountInboxByStateAsync(
            _capabilities.ProviderInstanceId,
            InboxMessageStates.DeadLettered,
            cancellationToken);

        return (prepared, processed, Math.Max(0, afterDead - beforeDead));
    }
}

/// <summary>Development helper: seed a qualified Job whose Vendor is not Proof360-approved.</summary>
public sealed class SeedUnapprovedDispatchDemoHandler
{
    private readonly ICanonicalWriter _canonical;
    private readonly IConnectorUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    /// <summary>Creates the handler.</summary>
    public SeedUnapprovedDispatchDemoHandler(
        ICanonicalWriter canonical,
        IConnectorUnitOfWork unitOfWork,
        IClock clock)
    {
        _canonical = canonical;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <summary>Seeds PendingReview Vendor + qualified Job for approval-gate demos.</summary>
    public async Task<Result<SeedDispatchDemoOutcome, IntegrationFailure>> HandleAsync(
        CancellationToken cancellationToken)
    {
        var vendorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var jobId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        var existingJob = await _canonical.FindJobAsync(jobId, cancellationToken);
        if (existingJob is not null)
        {
            return Result<SeedDispatchDemoOutcome, IntegrationFailure>.Ok(
                new SeedDispatchDemoOutcome(jobId, vendorId, Created: false));
        }

        if (await _canonical.FindVendorAsync(vendorId, cancellationToken) is null)
        {
            await _canonical.AddVendorAsync(
                new Vendor
                {
                    VendorId = vendorId,
                    ComplianceId = "CMP-UNAPPROVED",
                    LicenseNumber = "LIC-UNAPPROVED",
                    LicenseExpiry = new DateOnly(2030, 1, 1),
                    InsurancePolicy = "INS-UNAPPROVED",
                    InsuranceExpiry = new DateOnly(2030, 1, 1),
                    InsuranceCoverage = "1000000 CAD",
                    WcbNumber = "WCB-UNAPPROVED",
                    Status = VendorStatuses.PendingReview,
                    Rationale = "Demo seed: provider-active does not auto-approve.",
                    CreatedAt = _clock.UtcNow
                },
                cancellationToken);
        }

        await _canonical.AddJobAsync(
            new Job
            {
                JobId = jobId,
                Source = JobSources.Proof360,
                CustomerName = "Unapproved Dispatch Customer",
                CustomerPhone = "555-0144",
                AddressStreet = "44 Gate Street",
                AddressCity = "Calgary",
                AddressPostal = "T2P1J9",
                ServiceType = "plumbing",
                WindowStart = _clock.UtcNow.AddDays(1),
                WindowEnd = _clock.UtcNow.AddDays(1).AddHours(2),
                NotesScope = "Approval gate demo",
                Status = JobStatuses.Qualified,
                AssignedVendorId = vendorId
            },
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<SeedDispatchDemoOutcome, IntegrationFailure>.Ok(
            new SeedDispatchDemoOutcome(jobId, vendorId, Created: true));
    }
}

/// <summary>Development helper: seed a second qualified Job for ambiguous-POST demos.</summary>
public sealed class SeedAmbiguousDispatchDemoHandler
{
    private readonly ICanonicalWriter _canonical;
    private readonly IIntegrationStore _store;
    private readonly IConnectorUnitOfWork _unitOfWork;
    private readonly IProviderCapabilities _capabilities;
    private readonly IClock _clock;

    /// <summary>Creates the handler.</summary>
    public SeedAmbiguousDispatchDemoHandler(
        ICanonicalWriter canonical,
        IIntegrationStore store,
        IConnectorUnitOfWork unitOfWork,
        IProviderCapabilities capabilities,
        IClock clock)
    {
        _canonical = canonical;
        _store = store;
        _unitOfWork = unitOfWork;
        _capabilities = capabilities;
        _clock = clock;
    }

    /// <summary>Seeds an approved Vendor (if needed) and a fresh qualified Job.</summary>
    public async Task<Result<SeedDispatchDemoOutcome, IntegrationFailure>> HandleAsync(
        CancellationToken cancellationToken)
    {
        var vendorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var jobId = Guid.Parse("55555555-5555-5555-5555-555555555555");

        var existingJob = await _canonical.FindJobAsync(jobId, cancellationToken);
        if (existingJob is not null)
        {
            return Result<SeedDispatchDemoOutcome, IntegrationFailure>.Ok(
                new SeedDispatchDemoOutcome(jobId, vendorId, Created: false));
        }

        if (await _canonical.FindVendorAsync(vendorId, cancellationToken) is null)
        {
            await _canonical.AddVendorAsync(
                new Vendor
                {
                    VendorId = vendorId,
                    ComplianceId = "CMP-DEMO",
                    LicenseNumber = "LIC-DEMO",
                    LicenseExpiry = new DateOnly(2030, 1, 1),
                    InsurancePolicy = "INS-DEMO",
                    InsuranceExpiry = new DateOnly(2030, 1, 1),
                    InsuranceCoverage = "2000000 CAD",
                    WcbNumber = "WCB-DEMO",
                    Status = VendorStatuses.Approved,
                    Rationale = "Demo seed: Proof360-approved for local dispatch.",
                    CreatedAt = _clock.UtcNow
                },
                cancellationToken);
        }

        const string demoContractorExternalId = "ctr-demo-dispatch";
        if (await _store.FindIdentityByExternalAsync(
                _capabilities.ProviderInstanceId,
                ExternalEntityTypes.Contractor,
                demoContractorExternalId,
                cancellationToken) is null)
        {
            await _store.AddIdentityLinkAsync(
                new ProviderIdentityLink
                {
                    Id = Guid.NewGuid(),
                    ProviderName = ProviderNames.FieldFlow,
                    ProviderInstanceId = _capabilities.ProviderInstanceId,
                    ExternalEntityType = ExternalEntityTypes.Contractor,
                    ExternalId = demoContractorExternalId,
                    CanonicalEntityType = CanonicalEntityTypes.Vendor,
                    CanonicalId = vendorId,
                    LastAppliedAt = _clock.UtcNow
                },
                cancellationToken);
        }

        await _canonical.AddJobAsync(
            new Job
            {
                JobId = jobId,
                Source = JobSources.Proof360,
                CustomerName = "Ambiguous Post Customer",
                CustomerPhone = "555-0155",
                AddressStreet = "55 Ambiguous Ave",
                AddressCity = "Calgary",
                AddressPostal = "T2P1J9",
                ServiceType = "electrical",
                WindowStart = _clock.UtcNow.AddDays(2),
                WindowEnd = _clock.UtcNow.AddDays(2).AddHours(2),
                NotesScope = "Ambiguous POST reconcile demo",
                Status = JobStatuses.Qualified,
                AssignedVendorId = vendorId
            },
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<SeedDispatchDemoOutcome, IntegrationFailure>.Ok(
            new SeedDispatchDemoOutcome(jobId, vendorId, Created: true));
    }
}
