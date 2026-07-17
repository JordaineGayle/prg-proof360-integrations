using PRG.Proof360.Integrations.Domain.Canonical;
using PRG.Proof360.Integrations.Domain.Policies;

namespace PRG.Proof360.Integrations.UnitTests.Policies;

public sealed class VendorApprovalPolicyTests
{
    private readonly VendorApprovalPolicy _policy = new();

    [Fact]
    public void Provider_active_does_not_auto_approve()
    {
        var decision = _policy.Evaluate(
            currentStatus: null,
            providerIndicatesActive: true,
            providerIndicatesRestricted: false,
            proof360ExplicitlyApproved: false);

        Assert.Equal(VendorStatuses.PendingReview, decision.Status);
    }

    [Fact]
    public void Provider_restriction_is_safe_deny()
    {
        var decision = _policy.Evaluate(
            currentStatus: VendorStatuses.Approved,
            providerIndicatesActive: true,
            providerIndicatesRestricted: true,
            proof360ExplicitlyApproved: true);

        Assert.Equal(VendorStatuses.Restricted, decision.Status);
    }

    [Fact]
    public void Provider_reactivation_cannot_restore_approval_without_proof360_gate()
    {
        var decision = _policy.Evaluate(
            currentStatus: VendorStatuses.Restricted,
            providerIndicatesActive: true,
            providerIndicatesRestricted: false,
            proof360ExplicitlyApproved: false);

        Assert.Equal(VendorStatuses.PendingReview, decision.Status);
        Assert.DoesNotContain("auto-approve", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }
}
