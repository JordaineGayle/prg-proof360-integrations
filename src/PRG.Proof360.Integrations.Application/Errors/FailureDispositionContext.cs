namespace PRG.Proof360.Integrations.Application.Errors;

/// <summary>
/// Attempt/age context for <see cref="FailureDispositionPolicy"/>.
/// </summary>
/// <param name="AttemptCount">Current attempt count (1-based after first failure).</param>
/// <param name="FirstSeenAt">When the work item was first observed.</param>
/// <param name="UtcNow">Current UTC clock.</param>
/// <param name="MaxAttempts">Attempt budget before dead-letter.</param>
/// <param name="MaxAge">Age budget before dead-letter.</param>
/// <param name="DefaultRetryDelay">Default retry delay when provider does not supply Retry-After.</param>
/// <param name="DependencyRetryDelay">Delay used for WaitForDependency.</param>
public sealed record FailureDispositionContext(
    int AttemptCount,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset UtcNow,
    int MaxAttempts = 8,
    TimeSpan? MaxAge = null,
    TimeSpan? DefaultRetryDelay = null,
    TimeSpan? DependencyRetryDelay = null)
{
    /// <summary>Effective max age (default 24 hours).</summary>
    public TimeSpan EffectiveMaxAge => MaxAge ?? TimeSpan.FromHours(24);

    /// <summary>Effective default retry delay (default 30 seconds).</summary>
    public TimeSpan EffectiveDefaultRetryDelay => DefaultRetryDelay ?? TimeSpan.FromSeconds(30);

    /// <summary>Effective dependency retry delay (default 2 minutes).</summary>
    public TimeSpan EffectiveDependencyRetryDelay => DependencyRetryDelay ?? TimeSpan.FromMinutes(2);

    /// <summary>True when attempts or age are exhausted.</summary>
    public bool IsExhausted =>
        AttemptCount >= MaxAttempts ||
        UtcNow - FirstSeenAt >= EffectiveMaxAge;
}
