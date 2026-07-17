using System.ComponentModel.DataAnnotations;

namespace PRG.Proof360.Integrations.FieldFlow.Resilience;

/// <summary>
/// Configurable FieldFlow HTTP resilience thresholds. Bound from <c>FieldFlow:Resilience</c>.
/// </summary>
public sealed class FieldFlowResilienceOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "FieldFlow:Resilience";

    /// <summary>Per-attempt timeout in milliseconds.</summary>
    [Range(50, 120_000)]
    public int AttemptTimeoutMilliseconds { get; set; } = 3_000;

    /// <summary>
    /// Maximum retry attempts after the first try (total attempts = 1 + this value).
    /// </summary>
    [Range(0, 10)]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Base delay for exponential backoff (milliseconds).</summary>
    [Range(0, 60_000)]
    public int BaseDelayMilliseconds { get; set; } = 200;

    /// <summary>Maximum backoff delay excluding Retry-After (milliseconds).</summary>
    [Range(0, 300_000)]
    public int MaxDelayMilliseconds { get; set; } = 5_000;

    /// <summary>Upper bound applied to provider <c>Retry-After</c> (milliseconds).</summary>
    [Range(0, 300_000)]
    public int MaxRetryAfterMilliseconds { get; set; } = 30_000;

    /// <summary>
    /// When true, retry delays (including Retry-After) are forced to zero.
    /// Intended for deterministic automated tests only.
    /// </summary>
    public bool DisableRetryDelays { get; set; }

    /// <summary>Circuit failure ratio within the sampling window (0–1).</summary>
    [Range(0.01, 1.0)]
    public double CircuitFailureRatio { get; set; } = 0.5;

    /// <summary>Minimum samples before the circuit may open.</summary>
    [Range(2, 10_000)]
    public int CircuitMinimumThroughput { get; set; } = 5;

    /// <summary>Circuit sampling window in seconds.</summary>
    [Range(1, 3600)]
    public int CircuitSamplingDurationSeconds { get; set; } = 30;

    /// <summary>How long the circuit stays open before a half-open probe.</summary>
    [Range(1, 3600)]
    public int CircuitBreakDurationSeconds { get; set; } = 15;

    /// <summary>Optional concurrency limiter (bulkhead) permit count.</summary>
    [Range(1, 10_000)]
    public int ConcurrencyLimit { get; set; } = 64;

    /// <summary>Maximum concurrent queue waiting for a permit.</summary>
    [Range(0, 10_000)]
    public int ConcurrencyQueueLimit { get; set; } = 128;

    /// <summary>
    /// Documented worst-case HTTP attempts for one resilience-wrapped call:
    /// <c>1 + MaxRetryAttempts</c>.
    /// </summary>
    public int MaxAttemptsPerHttpCall => 1 + MaxRetryAttempts;
}
