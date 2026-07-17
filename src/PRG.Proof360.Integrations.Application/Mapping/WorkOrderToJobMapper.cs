using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Domain.Canonical;
using PRG.Proof360.Integrations.Domain.Policies;

namespace PRG.Proof360.Integrations.Application.Mapping;

/// <summary>
/// Pure mapper from provider-neutral <see cref="WorkOrderSnapshot"/> to canonical <see cref="Job"/>.
/// Does not query persistence; assigned vendor ids must be resolved by Application first.
/// </summary>
public sealed class WorkOrderToJobMapper
{
    private readonly WorkOrderStatusMappingPolicy _statusMapping;
    private readonly JobStatusTransitionPolicy _transitionPolicy;

    /// <summary>Creates the mapper.</summary>
    public WorkOrderToJobMapper(
        WorkOrderStatusMappingPolicy statusMapping,
        JobStatusTransitionPolicy transitionPolicy)
    {
        _statusMapping = statusMapping;
        _transitionPolicy = transitionPolicy;
    }

    /// <summary>
    /// Initializes a FieldFlow-originated Job from a snapshot.
    /// </summary>
    public JobMappingResult MapNewFromProvider(
        WorkOrderSnapshot snapshot,
        Guid jobId,
        Guid? assignedVendorId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var status = _statusMapping.MapFieldFlow(snapshot.ProviderStatus);
        if (!status.IsValid)
        {
            return JobMappingResult.Failed(status.Reason!);
        }

        var job = new Job
        {
            JobId = jobId,
            Source = JobSources.FieldFlow,
            TranscriptId = null,
            CustomerName = Normalize(snapshot.CustomerName),
            CustomerPhone = Normalize(snapshot.CustomerPhone),
            CustomerEmail = Normalize(snapshot.CustomerEmail),
            AddressStreet = Normalize(snapshot.AddressStreet),
            AddressUnit = Normalize(snapshot.AddressUnit),
            AddressCity = Normalize(snapshot.AddressCity),
            AddressPostal = Normalize(snapshot.AddressPostal),
            ServiceType = Normalize(snapshot.ServiceType),
            Subcategory = Normalize(snapshot.Subcategory),
            Priority = null,
            WindowStart = snapshot.WindowStart?.ToUniversalTime(),
            WindowEnd = snapshot.WindowEnd?.ToUniversalTime(),
            NotesScope = Normalize(snapshot.Notes),
            ComplianceOnly = false,
            Status = status.JobStatus!,
            AssignedVendorId = assignedVendorId,
            AiConfidence = null,
            AiJson = null
        };

        return JobMappingResult.Succeeded(job, ignoredOwnershipFields: []);
    }

    /// <summary>
    /// Applies a provider snapshot to an existing Job using origin-dependent ownership.
    /// </summary>
    public JobMappingResult MergeUpdate(
        Job existing,
        WorkOrderSnapshot snapshot,
        Guid? assignedVendorId)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(snapshot);

        var status = _statusMapping.MapFieldFlow(snapshot.ProviderStatus);
        if (!status.IsValid)
        {
            return JobMappingResult.Failed(status.Reason!);
        }

        var transition = _transitionPolicy.Evaluate(existing.Status, status.JobStatus);
        if (!transition.IsAllowed && !transition.IsNoOp)
        {
            return JobMappingResult.Failed(transition.Reason);
        }

        var ignored = new List<string>();
        var proof360Owned = string.Equals(existing.Source, JobSources.Proof360, StringComparison.Ordinal);

        if (proof360Owned)
        {
            // Provider echo cannot overwrite Proof360-owned fields.
            IgnoreIfDifferent(ignored, "customer_name", existing.CustomerName, snapshot.CustomerName);
            IgnoreIfDifferent(ignored, "customer_phone", existing.CustomerPhone, snapshot.CustomerPhone);
            IgnoreIfDifferent(ignored, "customer_email", existing.CustomerEmail, snapshot.CustomerEmail);
            IgnoreIfDifferent(ignored, "address_street", existing.AddressStreet, snapshot.AddressStreet);
            IgnoreIfDifferent(ignored, "address_unit", existing.AddressUnit, snapshot.AddressUnit);
            IgnoreIfDifferent(ignored, "address_city", existing.AddressCity, snapshot.AddressCity);
            IgnoreIfDifferent(ignored, "address_postal", existing.AddressPostal, snapshot.AddressPostal);
            IgnoreIfDifferent(ignored, "service_type", existing.ServiceType, snapshot.ServiceType);
            IgnoreIfDifferent(ignored, "subcategory", existing.Subcategory, snapshot.Subcategory);
            IgnoreIfDifferent(ignored, "priority", existing.Priority, null);
            IgnoreIfDifferent(ignored, "notes_scope", existing.NotesScope, snapshot.Notes);
            IgnoreWindow(ignored, "window_start", existing.WindowStart, snapshot.WindowStart);
            IgnoreWindow(ignored, "window_end", existing.WindowEnd, snapshot.WindowEnd);
            ignored.Add("compliance_only");
        }
        else
        {
            existing.CustomerName = Normalize(snapshot.CustomerName) ?? existing.CustomerName;
            existing.CustomerPhone = Normalize(snapshot.CustomerPhone) ?? existing.CustomerPhone;
            existing.CustomerEmail = Normalize(snapshot.CustomerEmail) ?? existing.CustomerEmail;
            existing.AddressStreet = Normalize(snapshot.AddressStreet) ?? existing.AddressStreet;
            existing.AddressUnit = Normalize(snapshot.AddressUnit) ?? existing.AddressUnit;
            existing.AddressCity = Normalize(snapshot.AddressCity) ?? existing.AddressCity;
            existing.AddressPostal = Normalize(snapshot.AddressPostal) ?? existing.AddressPostal;
            existing.ServiceType = Normalize(snapshot.ServiceType) ?? existing.ServiceType;
            existing.Subcategory = Normalize(snapshot.Subcategory) ?? existing.Subcategory;
            existing.NotesScope = Normalize(snapshot.Notes) ?? existing.NotesScope;
            existing.WindowStart = snapshot.WindowStart?.ToUniversalTime() ?? existing.WindowStart;
            existing.WindowEnd = snapshot.WindowEnd?.ToUniversalTime() ?? existing.WindowEnd;
        }

        if (transition.IsAllowed)
        {
            existing.Status = status.JobStatus!;
        }

        if (assignedVendorId is not null)
        {
            existing.AssignedVendorId = assignedVendorId;
        }

        // Never fabricate AI fields.
        return JobMappingResult.Succeeded(existing, ignored);
    }

    private static void IgnoreIfDifferent(
        List<string> ignored,
        string field,
        string? existing,
        string? incoming)
    {
        var normalizedIncoming = Normalize(incoming);
        if (normalizedIncoming is null)
        {
            return;
        }

        if (!string.Equals(existing, normalizedIncoming, StringComparison.Ordinal))
        {
            ignored.Add(field);
        }
    }

    private static void IgnoreWindow(
        List<string> ignored,
        string field,
        DateTimeOffset? existing,
        DateTimeOffset? incoming)
    {
        if (incoming is null)
        {
            return;
        }

        if (existing != incoming.Value.ToUniversalTime())
        {
            ignored.Add(field);
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Result of mapping a work-order snapshot onto a Job.
/// </summary>
public sealed class JobMappingResult
{
    private JobMappingResult(bool isSuccess, Job? job, string? error, IReadOnlyList<string> ignoredOwnershipFields)
    {
        IsSuccess = isSuccess;
        Job = job;
        Error = error;
        IgnoredOwnershipFields = ignoredOwnershipFields;
    }

    /// <summary>Whether mapping succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Mapped job when successful.</summary>
    public Job? Job { get; }

    /// <summary>Failure reason.</summary>
    public string? Error { get; }

    /// <summary>Proof360-owned fields ignored from the provider payload.</summary>
    public IReadOnlyList<string> IgnoredOwnershipFields { get; }

    /// <summary>Success.</summary>
    public static JobMappingResult Succeeded(Job job, IReadOnlyList<string> ignoredOwnershipFields) =>
        new(true, job, null, ignoredOwnershipFields);

    /// <summary>Failure.</summary>
    public static JobMappingResult Failed(string error) => new(false, null, error, []);
}
