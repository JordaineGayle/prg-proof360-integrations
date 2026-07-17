namespace PRG.Proof360.Integrations.Application.Errors;

/// <summary>
/// Durable worker disposition for a typed failure. Centralized; not a scattered IsRetryable flag.
/// </summary>
public abstract record FailureDisposition
{
    private FailureDisposition()
    {
    }

    /// <summary>Work is complete as a handled outcome (rare for failures; usually success outcomes).</summary>
    /// <param name="OutcomeCode">Stable outcome code.</param>
    public sealed record CompleteAsHandled(string OutcomeCode) : FailureDisposition;

    /// <summary>Retry the work at a specific UTC time.</summary>
    /// <param name="At">When to retry.</param>
    /// <param name="ReasonCode">Stable reason code.</param>
    public sealed record RetryAt(DateTimeOffset At, string ReasonCode) : FailureDisposition;

    /// <summary>Wait for a dependency (for example contractor sync).</summary>
    /// <param name="At">Next check time.</param>
    /// <param name="DependencyCode">Dependency identifier/code.</param>
    public sealed record WaitForDependency(DateTimeOffset At, string DependencyCode) : FailureDisposition;

    /// <summary>Dead-letter; no blind retry.</summary>
    /// <param name="ReasonCode">Stable reason code.</param>
    public sealed record DeadLetter(string ReasonCode) : FailureDisposition;

    /// <summary>Needs human/ops attention.</summary>
    /// <param name="ReasonCode">Stable reason code.</param>
    public sealed record NeedsAttention(string ReasonCode) : FailureDisposition;
}
