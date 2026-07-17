using PRG.Proof360.Integrations.Application.Dispatch;
using PRG.Proof360.Integrations.Application.Errors;
using PRG.Proof360.Integrations.Domain.Canonical;

namespace PRG.Proof360.Integrations.UnitTests.Dispatch;

public sealed class JobDispatchEligibilityTests
{
    [Fact]
    public void Qualified_approved_job_is_eligible()
    {
        var failure = JobDispatchEligibility.Evaluate(QualifiedJob(), ApprovedVendor());
        Assert.Null(failure);
    }

    [Fact]
    public void Non_qualified_status_is_rejected()
    {
        var job = QualifiedJob();
        job.Status = JobStatuses.Dispatched;
        var failure = JobDispatchEligibility.Evaluate(job, ApprovedVendor());
        Assert.NotNull(failure);
        Assert.Equal(FailureCodes.JobNotQualified, failure!.Code);
    }

    [Fact]
    public void Restricted_vendor_is_rejected()
    {
        var vendor = ApprovedVendor();
        vendor.Status = VendorStatuses.Restricted;
        var failure = JobDispatchEligibility.Evaluate(QualifiedJob(), vendor);
        Assert.Equal(FailureCodes.VendorNotApproved, failure!.Code);
    }

    private static Job QualifiedJob() => new()
    {
        JobId = Guid.NewGuid(),
        Status = JobStatuses.Qualified,
        CustomerName = "Ada",
        AddressStreet = "1 St",
        AddressCity = "Calgary",
        ServiceType = "plumbing",
        NotesScope = "scope",
        AssignedVendorId = Guid.NewGuid()
    };

    private static Vendor ApprovedVendor() => new()
    {
        VendorId = Guid.NewGuid(),
        Status = VendorStatuses.Approved,
        CreatedAt = DateTimeOffset.UtcNow
    };
}
