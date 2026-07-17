using PRG.Proof360.Integrations.Application.Errors;
using PRG.Proof360.Integrations.Domain.Canonical;

namespace PRG.Proof360.Integrations.Application.Dispatch;

/// <summary>
/// Pure eligibility checks for queuing a Job for FieldFlow dispatch.
/// Does not call providers and does not auto-approve from provider-active signals.
/// </summary>
public static class JobDispatchEligibility
{
    /// <summary>
    /// Validates Job status, required fields, and Vendor approval gate.
    /// </summary>
    public static IntegrationFailure? Evaluate(Job job, Vendor? vendor)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (!string.Equals(job.Status, JobStatuses.Qualified, StringComparison.Ordinal))
        {
            return new IntegrationFailure(
                FailureCodes.JobNotQualified,
                "Job status must be qualified before dispatch.",
                FailureCategory.Approval);
        }

        if (string.IsNullOrWhiteSpace(job.CustomerName) ||
            string.IsNullOrWhiteSpace(job.AddressStreet) ||
            string.IsNullOrWhiteSpace(job.AddressCity) ||
            string.IsNullOrWhiteSpace(job.ServiceType))
        {
            return IntegrationFailure.Validation(
                FailureCodes.RequiredFieldMissing,
                "Customer name, address street, address city, and service type are required for dispatch.");
        }

        if (job.WindowStart is null && job.WindowEnd is null && string.IsNullOrWhiteSpace(job.NotesScope))
        {
            return IntegrationFailure.Validation(
                FailureCodes.RequiredFieldMissing,
                "A service window or notes scope is required for dispatch.");
        }

        if (job.AssignedVendorId is null)
        {
            return new IntegrationFailure(
                FailureCodes.VendorNotApproved,
                "Job must have an assigned Vendor before dispatch.",
                FailureCategory.Approval);
        }

        if (vendor is null)
        {
            return new IntegrationFailure(
                FailureCodes.VendorNotFound,
                "Assigned Vendor was not found.",
                FailureCategory.NotFound);
        }

        if (string.Equals(vendor.Status, VendorStatuses.Restricted, StringComparison.Ordinal))
        {
            return new IntegrationFailure(
                FailureCodes.VendorNotApproved,
                "Assigned Vendor is restricted and cannot be dispatched.",
                FailureCategory.Approval);
        }

        if (!string.Equals(vendor.Status, VendorStatuses.Approved, StringComparison.Ordinal))
        {
            return new IntegrationFailure(
                FailureCodes.ApprovalRequired,
                "Assigned Vendor must be Proof360-approved before dispatch.",
                FailureCategory.Approval);
        }

        return null;
    }
}
