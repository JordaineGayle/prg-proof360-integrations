using PRG.Proof360.Integrations.Application.Abstractions.Time;
using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Domain.Canonical;
using PRG.Proof360.Integrations.Domain.Policies;

namespace PRG.Proof360.Integrations.Application.Mapping;

/// <summary>
/// Pure mapper from provider-neutral <see cref="ContractorSnapshot"/> to canonical <see cref="Vendor"/>.
/// Does not query persistence.
/// </summary>
public sealed class ContractorToVendorMapper
{
    private readonly ComplianceMissingItemsCalculator _missingItems;
    private readonly VendorApprovalPolicy _approvalPolicy;
    private readonly IClock _clock;

    /// <summary>Creates the mapper.</summary>
    public ContractorToVendorMapper(
        ComplianceMissingItemsCalculator missingItems,
        VendorApprovalPolicy approvalPolicy,
        IClock clock)
    {
        _missingItems = missingItems;
        _approvalPolicy = approvalPolicy;
        _clock = clock;
    }

    /// <summary>
    /// Maps a first-time contractor import. Status remains pending_review unless restricted by compliance.
    /// </summary>
    public Vendor MapNew(ContractorSnapshot snapshot, Guid vendorId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var asOf = _clock.UtcToday;
        var expired = _missingItems.HasExpiredCompliance(snapshot, asOf);
        var missing = _missingItems.Calculate(snapshot);

        // Missing items alone stay pending_review; expired compliance is safe-deny (restricted).
        var decision = _approvalPolicy.Evaluate(
            currentStatus: null,
            providerIndicatesActive: snapshot.IsActive,
            providerIndicatesRestricted: expired,
            proof360ExplicitlyApproved: false);

        return new Vendor
        {
            VendorId = vendorId,
            ComplianceId = Normalize(snapshot.ComplianceId),
            LicenseNumber = Normalize(snapshot.LicenseNumber),
            LicenseExpiry = snapshot.LicenseExpiry,
            InsurancePolicy = Normalize(snapshot.InsurancePolicy),
            InsuranceExpiry = snapshot.InsuranceExpiry,
            InsuranceCoverage = Normalize(snapshot.InsuranceCoverage),
            WcbNumber = Normalize(snapshot.WcbNumber),
            Status = decision.Status,
            AiConfidence = null,
            MissingItems = missing,
            Rationale = decision.Reason,
            CreatedAt = _clock.UtcNow
        };
    }

    /// <summary>
    /// Merges provider snapshot into an existing Vendor. Never fabricates AI values.
    /// <see cref="Vendor.CreatedAt"/> is preserved.
    /// </summary>
    public Vendor MergeExisting(
        Vendor existing,
        ContractorSnapshot snapshot,
        bool proof360ExplicitlyApproved)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(snapshot);

        var asOf = _clock.UtcToday;
        var expired = _missingItems.HasExpiredCompliance(snapshot, asOf);
        var missing = _missingItems.Calculate(snapshot);

        var decision = _approvalPolicy.Evaluate(
            currentStatus: existing.Status,
            providerIndicatesActive: snapshot.IsActive,
            providerIndicatesRestricted: expired,
            proof360ExplicitlyApproved: proof360ExplicitlyApproved);

        existing.ComplianceId = Normalize(snapshot.ComplianceId) ?? existing.ComplianceId;
        existing.LicenseNumber = Normalize(snapshot.LicenseNumber) ?? existing.LicenseNumber;
        existing.LicenseExpiry = snapshot.LicenseExpiry ?? existing.LicenseExpiry;
        existing.InsurancePolicy = Normalize(snapshot.InsurancePolicy) ?? existing.InsurancePolicy;
        existing.InsuranceExpiry = snapshot.InsuranceExpiry ?? existing.InsuranceExpiry;
        existing.InsuranceCoverage = Normalize(snapshot.InsuranceCoverage) ?? existing.InsuranceCoverage;
        existing.WcbNumber = Normalize(snapshot.WcbNumber) ?? existing.WcbNumber;
        existing.Status = decision.Status;
        existing.MissingItems = missing;
        existing.Rationale = decision.Reason;
        // AI fields intentionally untouched / left null when absent.
        return existing;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
