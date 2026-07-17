using PRG.Proof360.Integrations.Domain.Canonical;

namespace PRG.Proof360.Integrations.Application.Mapping;

/// <summary>
/// Maps FieldFlow work-order statuses to Proof360 Job statuses.
/// Unknown provider statuses are rejected with a stable classification.
/// </summary>
public sealed class WorkOrderStatusMappingPolicy
{
    private static readonly Dictionary<string, string> FieldFlowMap = new(StringComparer.Ordinal)
    {
        ["open"] = JobStatuses.Dispatched,
        ["scheduled"] = JobStatuses.Scheduled,
        ["in_progress"] = JobStatuses.InProgress,
        ["done"] = JobStatuses.Completed,
        ["void"] = JobStatuses.Cancelled
    };

    /// <summary>
    /// Attempts to map a FieldFlow provider status to a Job status.
    /// </summary>
    public StatusMappingResult MapFieldFlow(string? providerStatus)
    {
        if (string.IsNullOrWhiteSpace(providerStatus))
        {
            return StatusMappingResult.Invalid("Provider status is required.");
        }

        if (FieldFlowMap.TryGetValue(providerStatus.Trim(), out var mapped))
        {
            return StatusMappingResult.Mapped(mapped);
        }

        return StatusMappingResult.Invalid($"Unknown FieldFlow status '{providerStatus}'.");
    }
}

/// <summary>
/// Result of mapping a provider status to a Job status.
/// </summary>
public sealed class StatusMappingResult
{
    private StatusMappingResult(bool isValid, string? jobStatus, string? reason)
    {
        IsValid = isValid;
        JobStatus = jobStatus;
        Reason = reason;
    }

    /// <summary>Whether mapping succeeded.</summary>
    public bool IsValid { get; }

    /// <summary>Mapped Job status when valid.</summary>
    public string? JobStatus { get; }

    /// <summary>Failure reason when invalid.</summary>
    public string? Reason { get; }

    /// <summary>Creates a mapped result.</summary>
    public static StatusMappingResult Mapped(string jobStatus) => new(true, jobStatus, null);

    /// <summary>Creates an invalid result.</summary>
    public static StatusMappingResult Invalid(string reason) => new(false, null, reason);
}
