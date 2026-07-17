using PRG.Proof360.Integrations.Domain.Canonical;

namespace PRG.Proof360.Integrations.Domain.Policies;

/// <summary>
/// Encodes asymmetric Vendor approval rules: provider data may restrict, but cannot auto-approve.
/// </summary>
public sealed class VendorApprovalPolicy
{
    /// <summary>
    /// Resolves the Vendor status after observing provider active/restricted signals.
    /// </summary>
    /// <param name="currentStatus">Current Proof360 Vendor status, if any.</param>
    /// <param name="providerIndicatesActive">Whether the provider currently marks the contractor active.</param>
    /// <param name="providerIndicatesRestricted">Whether the provider or compliance checks require restriction.</param>
    /// <param name="proof360ExplicitlyApproved">Whether Proof360 has recorded an explicit approval decision.</param>
    public VendorStatusDecision Evaluate(
        string? currentStatus,
        bool providerIndicatesActive,
        bool providerIndicatesRestricted,
        bool proof360ExplicitlyApproved)
    {
        if (providerIndicatesRestricted)
        {
            return new VendorStatusDecision(
                VendorStatuses.Restricted,
                "Provider or compliance signal requires restriction (safe-deny).");
        }

        if (proof360ExplicitlyApproved &&
            string.Equals(currentStatus, VendorStatuses.Approved, StringComparison.Ordinal))
        {
            return new VendorStatusDecision(
                VendorStatuses.Approved,
                "Proof360 approval retained while provider remains unrestricted.");
        }

        if (providerIndicatesActive &&
            string.Equals(currentStatus, VendorStatuses.Approved, StringComparison.Ordinal) &&
            !proof360ExplicitlyApproved)
        {
            // Defensive: approved without explicit flag should not be invented by provider alone.
            return new VendorStatusDecision(
                VendorStatuses.PendingReview,
                "Provider active cannot auto-approve; Proof360 approval gate required.");
        }

        if (providerIndicatesActive &&
            !string.Equals(currentStatus, VendorStatuses.Approved, StringComparison.Ordinal))
        {
            return new VendorStatusDecision(
                VendorStatuses.PendingReview,
                "First/active import remains pending_review until Proof360 approves.");
        }

        if (string.Equals(currentStatus, VendorStatuses.Approved, StringComparison.Ordinal) &&
            proof360ExplicitlyApproved)
        {
            return new VendorStatusDecision(
                VendorStatuses.Approved,
                "Existing Proof360 approval retained.");
        }

        return new VendorStatusDecision(
            string.IsNullOrWhiteSpace(currentStatus) ? VendorStatuses.PendingReview : currentStatus,
            "No automatic approval from provider signals.");
    }
}

/// <summary>
/// Vendor status decision produced by <see cref="VendorApprovalPolicy"/>.
/// </summary>
/// <param name="Status">Resolved status.</param>
/// <param name="Reason">Audit rationale.</param>
public sealed record VendorStatusDecision(string Status, string Reason);
