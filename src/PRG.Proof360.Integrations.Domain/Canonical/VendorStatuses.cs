namespace PRG.Proof360.Integrations.Domain.Canonical;

/// <summary>
/// Assumed Vendor status vocabulary for Phase 1 approval asymmetry.
/// </summary>
public static class VendorStatuses
{
    /// <summary>Imported and awaiting Proof360 approval.</summary>
    public const string PendingReview = "pending_review";

    /// <summary>Explicitly approved by Proof360. Cannot be granted by provider reactivation alone.</summary>
    public const string Approved = "approved";

    /// <summary>Restricted due to provider suspension or expired compliance (safe-deny).</summary>
    public const string Restricted = "restricted";
}
