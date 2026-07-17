namespace PRG.FieldFlow.Mock.State;

/// <summary>
/// Deterministic, test-only failure injection counters. Not random.
/// </summary>
public sealed class FailureInjectionState
{
    private readonly object _gate = new();

    /// <summary>Remaining 429 responses.</summary>
    public int Remaining429 { get; private set; }

    /// <summary>Retry-After seconds for 429 responses.</summary>
    public int RetryAfterSeconds { get; private set; } = 1;

    /// <summary>Remaining 500 responses.</summary>
    public int Remaining500 { get; private set; }

    /// <summary>Remaining timeout simulations.</summary>
    public int RemainingTimeouts { get; private set; }

    /// <summary>Delay used when simulating client timeout.</summary>
    public int TimeoutDelayMilliseconds { get; private set; } = 5_000;

    /// <summary>When true, /health reports unavailable.</summary>
    public bool HealthUnavailable { get; private set; }

    /// <summary>When true, next successful POST create persists but drops/aborts the HTTP response.</summary>
    public bool AmbiguousNextPost { get; private set; }

    /// <summary>Configures failure injection.</summary>
    public void Configure(FailureInjectionRequest request)
    {
        lock (_gate)
        {
            if (request.RateLimitCount is not null)
            {
                Remaining429 = Math.Max(0, request.RateLimitCount.Value);
                RetryAfterSeconds = Math.Max(0, request.RetryAfterSeconds ?? RetryAfterSeconds);
            }

            if (request.ServerErrorCount is not null)
            {
                Remaining500 = Math.Max(0, request.ServerErrorCount.Value);
            }

            if (request.TimeoutCount is not null)
            {
                RemainingTimeouts = Math.Max(0, request.TimeoutCount.Value);
                TimeoutDelayMilliseconds = Math.Max(1, request.TimeoutDelayMilliseconds ?? TimeoutDelayMilliseconds);
            }

            if (request.HealthUnavailable is not null)
            {
                HealthUnavailable = request.HealthUnavailable.Value;
            }

            if (request.AmbiguousNextPost is not null)
            {
                AmbiguousNextPost = request.AmbiguousNextPost.Value;
            }
        }
    }

    /// <summary>Resets all injection counters.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            Remaining429 = 0;
            Remaining500 = 0;
            RemainingTimeouts = 0;
            HealthUnavailable = false;
            AmbiguousNextPost = false;
            RetryAfterSeconds = 1;
            TimeoutDelayMilliseconds = 5_000;
        }
    }

    /// <summary>Consumes the next injected failure, if any.</summary>
    public InjectedFailure? ConsumeProviderFailure()
    {
        lock (_gate)
        {
            if (Remaining429 > 0)
            {
                Remaining429--;
                return new InjectedFailure(InjectedFailureKind.RateLimited, RetryAfterSeconds, TimeoutDelayMilliseconds);
            }

            if (Remaining500 > 0)
            {
                Remaining500--;
                return new InjectedFailure(InjectedFailureKind.ServerError, RetryAfterSeconds, TimeoutDelayMilliseconds);
            }

            if (RemainingTimeouts > 0)
            {
                RemainingTimeouts--;
                return new InjectedFailure(InjectedFailureKind.Timeout, RetryAfterSeconds, TimeoutDelayMilliseconds);
            }

            return null;
        }
    }

    /// <summary>Consumes the ambiguous-post flag.</summary>
    public bool ConsumeAmbiguousPost()
    {
        lock (_gate)
        {
            if (!AmbiguousNextPost)
            {
                return false;
            }

            AmbiguousNextPost = false;
            return true;
        }
    }
}

/// <summary>Failure injection request body for <c>/_test/failures</c>.</summary>
public sealed class FailureInjectionRequest
{
    /// <summary>Number of subsequent provider calls that return 429.</summary>
    public int? RateLimitCount { get; set; }

    /// <summary>Retry-After seconds for 429.</summary>
    public int? RetryAfterSeconds { get; set; }

    /// <summary>Number of subsequent provider calls that return 500.</summary>
    public int? ServerErrorCount { get; set; }

    /// <summary>Number of subsequent provider calls that delay to exceed client timeouts.</summary>
    public int? TimeoutCount { get; set; }

    /// <summary>Delay milliseconds for timeout simulation.</summary>
    public int? TimeoutDelayMilliseconds { get; set; }

    /// <summary>Forces /health into an unavailable state.</summary>
    public bool? HealthUnavailable { get; set; }

    /// <summary>Next POST /work-orders persists then drops the response.</summary>
    public bool? AmbiguousNextPost { get; set; }
}

/// <summary>Kind of injected failure.</summary>
public enum InjectedFailureKind
{
    /// <summary>HTTP 429.</summary>
    RateLimited,

    /// <summary>HTTP 500.</summary>
    ServerError,

    /// <summary>Artificial delay.</summary>
    Timeout
}

/// <summary>Consumed failure instruction.</summary>
public sealed record InjectedFailure(InjectedFailureKind Kind, int RetryAfterSeconds, int TimeoutDelayMilliseconds);
