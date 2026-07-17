using System.Collections.Concurrent;

namespace PRG.Proof360.Integrations.FieldFlow.Resilience;

/// <summary>
/// Process-local FieldFlow resilience projection (circuit, freshness, sanitized failures).
/// Durable backlog counts come from the integration store at health-query time.
/// </summary>
public sealed class FieldFlowResilienceState
{
    private long _httpAttemptCount;
    private readonly object _gate = new();
    private readonly ConcurrentQueue<DateTimeOffset> _recentRateLimits = new();

    /// <summary>Current circuit state string.</summary>
    public string CircuitState { get; private set; } = FieldFlowCircuitStates.Closed;

    /// <summary>UTC of last successful provider HTTP outcome (after retries).</summary>
    public DateTimeOffset? LastSuccessfulProviderCallAt { get; private set; }

    /// <summary>UTC of last sanitized failure.</summary>
    public DateTimeOffset? LastFailureAt { get; private set; }

    /// <summary>Sanitized failure category (e.g. Unavailable, ProviderAuthentication).</summary>
    public string? LastFailureCategory { get; private set; }

    /// <summary>Sanitized failure code.</summary>
    public string? LastFailureCode { get; private set; }

    /// <summary>True when auth/config failures require operator attention.</summary>
    public bool NeedsAttention { get; private set; }

    /// <summary>Total HTTP attempts that reached the transport handler.</summary>
    public long HttpAttemptCount => Interlocked.Read(ref _httpAttemptCount);

    /// <summary>Records a transport-level HTTP attempt.</summary>
    public void RecordHttpAttempt() => Interlocked.Increment(ref _httpAttemptCount);

    /// <summary>Resets attempt counter (tests).</summary>
    public void ResetHttpAttemptCount() => Interlocked.Exchange(ref _httpAttemptCount, 0);

    /// <summary>Sets circuit to open.</summary>
    public void SetCircuitOpen(DateTimeOffset utcNow)
    {
        lock (_gate)
        {
            CircuitState = FieldFlowCircuitStates.Open;
            LastFailureAt = utcNow;
            LastFailureCategory = "Unavailable";
            LastFailureCode = "circuit_open";
        }
    }

    /// <summary>Sets circuit to half-open.</summary>
    public void SetCircuitHalfOpen()
    {
        lock (_gate)
        {
            CircuitState = FieldFlowCircuitStates.HalfOpen;
        }
    }

    /// <summary>Sets circuit to closed.</summary>
    public void SetCircuitClosed()
    {
        lock (_gate)
        {
            CircuitState = FieldFlowCircuitStates.Closed;
        }
    }

    /// <summary>Records a successful final provider call.</summary>
    public void RecordSuccess(DateTimeOffset utcNow)
    {
        lock (_gate)
        {
            LastSuccessfulProviderCallAt = utcNow;
            if (CircuitState == FieldFlowCircuitStates.HalfOpen)
            {
                CircuitState = FieldFlowCircuitStates.Closed;
            }
        }
    }

    /// <summary>Records a sanitized terminal failure after resilience exhausts.</summary>
    public void RecordFailure(string category, string code, DateTimeOffset utcNow, bool needsAttention = false)
    {
        lock (_gate)
        {
            LastFailureAt = utcNow;
            LastFailureCategory = category;
            LastFailureCode = code;
            if (needsAttention)
            {
                NeedsAttention = true;
            }

            if (string.Equals(category, "RateLimited", StringComparison.OrdinalIgnoreCase))
            {
                _recentRateLimits.Enqueue(utcNow);
            }
        }

        TrimRateLimits(utcNow);
    }

    /// <summary>Clears the NeedsAttention flag after credentials are repaired (ops/tests).</summary>
    public void ClearNeedsAttention()
    {
        lock (_gate)
        {
            NeedsAttention = false;
        }
    }

    /// <summary>Count of rate-limit outcomes in the last <paramref name="window"/>.</summary>
    public int CountRecentRateLimits(DateTimeOffset utcNow, TimeSpan window)
    {
        TrimRateLimits(utcNow, window);
        return _recentRateLimits.Count;
    }

    /// <summary>Resets process state (tests).</summary>
    public void Reset()
    {
        lock (_gate)
        {
            CircuitState = FieldFlowCircuitStates.Closed;
            LastSuccessfulProviderCallAt = null;
            LastFailureAt = null;
            LastFailureCategory = null;
            LastFailureCode = null;
            NeedsAttention = false;
            while (_recentRateLimits.TryDequeue(out _))
            {
            }
        }

        ResetHttpAttemptCount();
    }

    private void TrimRateLimits(DateTimeOffset utcNow, TimeSpan? window = null)
    {
        var cutoff = utcNow - (window ?? TimeSpan.FromMinutes(15));
        while (_recentRateLimits.TryPeek(out var oldest) && oldest < cutoff)
        {
            _recentRateLimits.TryDequeue(out _);
        }
    }
}
