using PRG.Proof360.Integrations.Application.Abstractions.Persistence;
using PRG.Proof360.Integrations.Application.Abstractions.Time;
using PRG.Proof360.Integrations.Application.Errors;
using PRG.Proof360.Integrations.Application.Mapping;
using PRG.Proof360.Integrations.Application.Outcomes;
using PRG.Proof360.Integrations.Core.Integration;
using PRG.Proof360.Integrations.Core.Providers;
using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Core.Results;
using PRG.Proof360.Integrations.Domain.Canonical;

namespace PRG.Proof360.Integrations.Application.Contractors;

/// <summary>
/// Stages canonical Vendor + identity link changes from a contractor snapshot (idempotent).
/// Caller commits via <see cref="IConnectorUnitOfWork"/> so inbox completion stays atomic.
/// </summary>
public sealed class ApplyContractorSnapshotHandler
{
    private readonly ICanonicalWriter _canonical;
    private readonly IIntegrationStore _store;
    private readonly ContractorToVendorMapper _mapper;
    private readonly IClock _clock;

    /// <summary>Creates the handler.</summary>
    public ApplyContractorSnapshotHandler(
        ICanonicalWriter canonical,
        IIntegrationStore store,
        ContractorToVendorMapper mapper,
        IClock clock)
    {
        _canonical = canonical;
        _store = store;
        _mapper = mapper;
        _clock = clock;
    }

    /// <summary>
    /// Stages Vendor/link upsert. Does not save changes.
    /// </summary>
    public async Task<Result<ApplyContractorOutcome, IntegrationFailure>> HandleAsync(
        ContractorSnapshot snapshot,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var link = await _store.FindIdentityByExternalAsync(
            snapshot.ProviderInstanceId,
            ExternalEntityTypes.Contractor,
            snapshot.ExternalContractorId,
            cancellationToken);

        if (link is not null &&
            snapshot.EntityVersion is { } incoming &&
            link.LastAppliedVersion is { } applied &&
            incoming <= applied)
        {
            await WriteAuditAsync(
                snapshot,
                correlationId,
                link.CanonicalId,
                "no_change",
                cancellationToken);
            return Result<ApplyContractorOutcome, IntegrationFailure>.Ok(
                new ApplyContractorOutcome.NoChange(link.CanonicalId));
        }

        if (link is null)
        {
            return await StageCreateAsync(snapshot, correlationId, cancellationToken);
        }

        return await StageUpdateAsync(snapshot, link, correlationId, cancellationToken);
    }

    private async Task<Result<ApplyContractorOutcome, IntegrationFailure>> StageCreateAsync(
        ContractorSnapshot snapshot,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var vendorId = Guid.NewGuid();
        var vendor = _mapper.MapNew(snapshot, vendorId);
        var newLink = new ProviderIdentityLink
        {
            Id = Guid.NewGuid(),
            ProviderName = ProviderNames.FieldFlow,
            ProviderInstanceId = snapshot.ProviderInstanceId,
            ExternalEntityType = ExternalEntityTypes.Contractor,
            ExternalId = snapshot.ExternalContractorId,
            CanonicalEntityType = CanonicalEntityTypes.Vendor,
            CanonicalId = vendorId,
            LastAppliedVersion = snapshot.EntityVersion,
            LastAppliedAt = _clock.UtcNow
        };

        await _canonical.AddVendorAsync(vendor, cancellationToken);
        await _store.AddIdentityLinkAsync(newLink, cancellationToken);

        var outcome = string.Equals(vendor.Status, VendorStatuses.Restricted, StringComparison.Ordinal)
            ? (ApplyContractorOutcome)new ApplyContractorOutcome.Restricted(vendorId)
            : new ApplyContractorOutcome.Created(vendorId);

        await WriteAuditAsync(
            snapshot,
            correlationId,
            vendorId,
            outcome is ApplyContractorOutcome.Restricted ? "restricted" : "created",
            cancellationToken);

        return Result<ApplyContractorOutcome, IntegrationFailure>.Ok(outcome);
    }

    private async Task<Result<ApplyContractorOutcome, IntegrationFailure>> StageUpdateAsync(
        ContractorSnapshot snapshot,
        ProviderIdentityLink link,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var vendor = await _canonical.FindVendorAsync(link.CanonicalId, cancellationToken);
        if (vendor is null)
        {
            return Result<ApplyContractorOutcome, IntegrationFailure>.Fail(
                new IntegrationFailure(
                    FailureCodes.VendorNotFound,
                    "Identity link pointed at a missing Vendor.",
                    FailureCategory.NotFound));
        }

        var beforeStatus = vendor.Status;
        var beforeHash = HashVendor(vendor);
        var approved = string.Equals(vendor.Status, VendorStatuses.Approved, StringComparison.Ordinal);
        _mapper.MergeExisting(vendor, snapshot, proof360ExplicitlyApproved: approved);

        link.LastAppliedVersion = snapshot.EntityVersion ?? link.LastAppliedVersion;
        link.LastAppliedAt = _clock.UtcNow;
        link.RowVersion += 1;

        var afterHash = HashVendor(vendor);
        ApplyContractorOutcome outcome;
        string auditResult;
        if (string.Equals(vendor.Status, VendorStatuses.Restricted, StringComparison.Ordinal) &&
            !string.Equals(beforeStatus, VendorStatuses.Restricted, StringComparison.Ordinal))
        {
            outcome = new ApplyContractorOutcome.Restricted(vendor.VendorId);
            auditResult = "restricted";
        }
        else if (beforeHash == afterHash)
        {
            outcome = new ApplyContractorOutcome.NoChange(vendor.VendorId);
            auditResult = "no_change";
        }
        else
        {
            outcome = new ApplyContractorOutcome.Updated(vendor.VendorId);
            auditResult = "updated";
        }

        await WriteAuditAsync(snapshot, correlationId, vendor.VendorId, auditResult, cancellationToken);
        return Result<ApplyContractorOutcome, IntegrationFailure>.Ok(outcome);
    }

    private Task WriteAuditAsync(
        ContractorSnapshot snapshot,
        string? correlationId,
        Guid canonicalId,
        string result,
        CancellationToken cancellationToken) =>
        _store.AddAuditEventAsync(
            new AuditEvent
            {
                Id = Guid.NewGuid(),
                CorrelationId = correlationId,
                Direction = "inbound",
                ProviderName = snapshot.ProviderName,
                ProviderInstanceId = snapshot.ProviderInstanceId,
                Operation = "contractor.apply",
                CanonicalEntityType = CanonicalEntityTypes.Vendor,
                CanonicalId = canonicalId,
                Result = result,
                SchemaVersion = snapshot.SchemaVersion,
                Timestamp = _clock.UtcNow
            },
            cancellationToken);

    private static string HashVendor(Vendor vendor) =>
        $"{vendor.ComplianceId}|{vendor.LicenseNumber}|{vendor.LicenseExpiry}|{vendor.InsurancePolicy}|{vendor.InsuranceExpiry}|{vendor.InsuranceCoverage}|{vendor.WcbNumber}|{vendor.Status}|{vendor.MissingItems}";
}
