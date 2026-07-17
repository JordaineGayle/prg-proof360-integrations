using PRG.Proof360.Integrations.Application.Abstractions.Persistence;
using PRG.Proof360.Integrations.Application.Abstractions.Time;
using PRG.Proof360.Integrations.Application.Errors;
using PRG.Proof360.Integrations.Application.Mapping;
using PRG.Proof360.Integrations.Core.Integration;
using PRG.Proof360.Integrations.Core.Providers;
using PRG.Proof360.Integrations.Core.Providers.Capabilities;
using PRG.Proof360.Integrations.Core.Results;
using PRG.Proof360.Integrations.Domain.Canonical;

namespace PRG.Proof360.Integrations.Application.Dispatch;

/// <summary>
/// Demo-only seed for a qualified Job + approved Vendor with contractor identity.
/// Not a production API surface.
/// </summary>
public sealed class SeedQualifiedDispatchDemoHandler
{
    private readonly ICanonicalWriter _canonical;
    private readonly IIntegrationStore _store;
    private readonly IConnectorUnitOfWork _unitOfWork;
    private readonly IProviderCapabilities _capabilities;
    private readonly IClock _clock;

    /// <summary>Creates the handler.</summary>
    public SeedQualifiedDispatchDemoHandler(
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

    /// <summary>
    /// Seeds an approved Vendor and qualified Proof360 Job ready for dispatch demos.
    /// </summary>
    public async Task<Result<SeedDispatchDemoOutcome, IntegrationFailure>> HandleAsync(
        CancellationToken cancellationToken)
    {
        var vendorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var jobId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var existingJob = await _canonical.FindJobAsync(jobId, cancellationToken);
        if (existingJob is not null)
        {
            return Result<SeedDispatchDemoOutcome, IntegrationFailure>.Ok(
                new SeedDispatchDemoOutcome(jobId, vendorId, Created: false));
        }

        // Dedicated demo contractor id — must not collide with mock fixture ctr-1001 after sync.
        const string demoContractorExternalId = "ctr-demo-dispatch";

        var existingVendor = await _canonical.FindVendorAsync(vendorId, cancellationToken);
        if (existingVendor is null)
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

        await _canonical.AddJobAsync(
            new Job
            {
                JobId = jobId,
                Source = JobSources.Proof360,
                CustomerName = "Demo Customer",
                CustomerPhone = "555-0100",
                AddressStreet = "100 Demo Street",
                AddressCity = "Calgary",
                AddressPostal = "T2P1J9",
                ServiceType = "plumbing",
                WindowStart = _clock.UtcNow.AddDays(1),
                WindowEnd = _clock.UtcNow.AddDays(1).AddHours(2),
                NotesScope = "Local demo dispatch seed",
                Status = JobStatuses.Qualified,
                AssignedVendorId = vendorId
            },
            cancellationToken);

        var contractorLink = await _store.FindIdentityByExternalAsync(
            _capabilities.ProviderInstanceId,
            ExternalEntityTypes.Contractor,
            demoContractorExternalId,
            cancellationToken);
        if (contractorLink is null)
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

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<SeedDispatchDemoOutcome, IntegrationFailure>.Ok(
            new SeedDispatchDemoOutcome(jobId, vendorId, Created: true));
    }
}

/// <summary>Demo seed result.</summary>
/// <param name="JobId">Seeded job id.</param>
/// <param name="VendorId">Seeded vendor id.</param>
/// <param name="Created">Whether rows were newly created.</param>
public sealed record SeedDispatchDemoOutcome(Guid JobId, Guid VendorId, bool Created);
