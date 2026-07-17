using PRG.Proof360.Integrations.Domain.Canonical;

namespace PRG.Proof360.Integrations.Domain.Policies;

/// <summary>
/// Evaluates monotonic Job operational status transitions.
/// Stale/equal versions are handled by application ordering; this policy validates status edges only.
/// </summary>
public sealed class JobStatusTransitionPolicy
{
    private static readonly HashSet<string> Terminal =
    [
        JobStatuses.Completed,
        JobStatuses.Cancelled
    ];

    private static readonly Dictionary<string, HashSet<string>> Allowed = new(StringComparer.Ordinal)
    {
        [JobStatuses.Qualified] = [JobStatuses.Dispatched, JobStatuses.Cancelled],
        [JobStatuses.Dispatched] = [JobStatuses.Scheduled, JobStatuses.InProgress, JobStatuses.Cancelled],
        [JobStatuses.Scheduled] = [JobStatuses.InProgress, JobStatuses.Cancelled],
        [JobStatuses.InProgress] = [JobStatuses.Completed, JobStatuses.Cancelled],
        [JobStatuses.Completed] = [],
        [JobStatuses.Cancelled] = []
    };

    /// <summary>
    /// Determines whether moving from <paramref name="fromStatus"/> to <paramref name="toStatus"/> is allowed.
    /// Equal statuses are treated as a no-op (not an error).
    /// </summary>
    public JobStatusTransitionResult Evaluate(string? fromStatus, string? toStatus)
    {
        if (string.IsNullOrWhiteSpace(fromStatus) || string.IsNullOrWhiteSpace(toStatus))
        {
            return JobStatusTransitionResult.Invalid("Status values are required.");
        }

        if (string.Equals(fromStatus, toStatus, StringComparison.Ordinal))
        {
            return JobStatusTransitionResult.NoOp("Status unchanged.");
        }

        if (!Allowed.ContainsKey(fromStatus) || !Allowed.ContainsKey(toStatus))
        {
            return JobStatusTransitionResult.Invalid("Unknown status value.");
        }

        if (Terminal.Contains(fromStatus))
        {
            return JobStatusTransitionResult.Invalid("Terminal statuses cannot transition in Phase 1.");
        }

        if (Allowed[fromStatus].Contains(toStatus))
        {
            return JobStatusTransitionResult.Allowed();
        }

        return JobStatusTransitionResult.Invalid("Transition is not permitted by the monotonic policy.");
    }
}

/// <summary>
/// Result of a Job status transition evaluation.
/// </summary>
public sealed class JobStatusTransitionResult
{
    private JobStatusTransitionResult(bool isAllowed, bool isNoOp, string reason)
    {
        IsAllowed = isAllowed;
        IsNoOp = isNoOp;
        Reason = reason;
    }

    /// <summary>Gets a value indicating whether the transition may mutate canonical state.</summary>
    public bool IsAllowed { get; }

    /// <summary>Gets a value indicating whether the request is an equal-status no-op.</summary>
    public bool IsNoOp { get; }

    /// <summary>Gets a stable human-readable reason for audit.</summary>
    public string Reason { get; }

    /// <summary>Creates an allowed result.</summary>
    public static JobStatusTransitionResult Allowed() => new(true, false, "Transition allowed.");

    /// <summary>Creates a no-op result.</summary>
    public static JobStatusTransitionResult NoOp(string reason) => new(false, true, reason);

    /// <summary>Creates an invalid result.</summary>
    public static JobStatusTransitionResult Invalid(string reason) => new(false, false, reason);
}
