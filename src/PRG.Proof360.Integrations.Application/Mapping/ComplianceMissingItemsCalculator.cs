using PRG.Proof360.Integrations.Core.Providers.Contracts;

namespace PRG.Proof360.Integrations.Application.Mapping;

/// <summary>
/// Deterministically calculates Vendor <c>missing_items</c> from a contractor snapshot.
/// </summary>
public sealed class ComplianceMissingItemsCalculator
{
    /// <summary>
    /// Returns a stable comma-separated list of missing compliance field names, or null when complete.
    /// </summary>
    public string? Calculate(ContractorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(snapshot.ComplianceId))
        {
            missing.Add("compliance_id");
        }

        if (string.IsNullOrWhiteSpace(snapshot.LicenseNumber))
        {
            missing.Add("license_number");
        }

        if (snapshot.LicenseExpiry is null)
        {
            missing.Add("license_expiry");
        }

        if (string.IsNullOrWhiteSpace(snapshot.InsurancePolicy))
        {
            missing.Add("insurance_policy");
        }

        if (snapshot.InsuranceExpiry is null)
        {
            missing.Add("insurance_expiry");
        }

        if (string.IsNullOrWhiteSpace(snapshot.InsuranceCoverage))
        {
            missing.Add("insurance_coverage");
        }

        if (string.IsNullOrWhiteSpace(snapshot.WcbNumber))
        {
            missing.Add("wcb_number");
        }

        return missing.Count == 0 ? null : string.Join(',', missing);
    }

    /// <summary>
    /// True when license or insurance is expired as of <paramref name="asOfDate"/> (safe-deny input).
    /// </summary>
    public bool HasExpiredCompliance(ContractorSnapshot snapshot, DateOnly asOfDate)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.LicenseExpiry is { } licenseExpiry && licenseExpiry < asOfDate)
        {
            return true;
        }

        if (snapshot.InsuranceExpiry is { } insuranceExpiry && insuranceExpiry < asOfDate)
        {
            return true;
        }

        return false;
    }
}
