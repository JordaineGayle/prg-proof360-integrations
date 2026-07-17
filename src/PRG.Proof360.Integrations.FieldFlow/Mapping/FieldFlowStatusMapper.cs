using PRG.Proof360.Integrations.Domain.Canonical;

namespace PRG.Proof360.Integrations.FieldFlow.Mapping;

/// <summary>
/// Documents FieldFlow status vocabulary at the ACL boundary.
/// Canonical Job status mapping for Application is owned by
/// <c>WorkOrderStatusMappingPolicy</c>; this type remains for adapter-local documentation/tests.
/// </summary>
public static class FieldFlowStatusMapper
{
    /// <summary>Known FieldFlow work-order statuses.</summary>
    public static IReadOnlyDictionary<string, string> ToJobStatus { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["open"] = JobStatuses.Dispatched,
            ["scheduled"] = JobStatuses.Scheduled,
            ["in_progress"] = JobStatuses.InProgress,
            ["done"] = JobStatuses.Completed,
            ["void"] = JobStatuses.Cancelled
        };

    /// <summary>Returns whether the provider status string is recognized.</summary>
    public static bool IsKnown(string? providerStatus) =>
        !string.IsNullOrWhiteSpace(providerStatus) && ToJobStatus.ContainsKey(providerStatus.Trim());
}
