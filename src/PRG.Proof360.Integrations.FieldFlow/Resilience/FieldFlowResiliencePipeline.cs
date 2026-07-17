using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Timeout;

namespace PRG.Proof360.Integrations.FieldFlow.Resilience;

/// <summary>
/// Configures the single FieldFlow HTTP resilience pipeline.
/// Addition order (first = outermost): concurrency → retry → circuit → attempt timeout.
/// </summary>
public static class FieldFlowResiliencePipeline
{
    /// <summary>Pipeline name registered with <c>AddResilienceHandler</c>.</summary>
    public const string PipelineName = "fieldflow";

    /// <summary>
    /// Configures concurrency limiter, retry, circuit breaker, and per-attempt timeout.
    /// </summary>
    public static void Configure(ResiliencePipelineBuilder<HttpResponseMessage> builder, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(services);

        var options = services.GetRequiredService<IOptionsMonitor<FieldFlowResilienceOptions>>().CurrentValue;
        var state = services.GetRequiredService<FieldFlowResilienceState>();
        var timeProvider = services.GetService<TimeProvider>() ?? TimeProvider.System;

        builder.AddConcurrencyLimiter(options.ConcurrencyLimit, options.ConcurrencyQueueLimit);

        // Polly requires MaxRetryAttempts >= 1 when the strategy is present; skip entirely for zero.
        if (options.MaxRetryAttempts > 0)
        {
            builder.AddRetry(CreateRetryOptions(options));
        }

        builder.AddCircuitBreaker(CreateCircuitOptions(options, state, timeProvider));

        builder.AddTimeout(new HttpTimeoutStrategyOptions
        {
            Timeout = TimeSpan.FromMilliseconds(options.AttemptTimeoutMilliseconds)
        });
    }

    /// <summary>
    /// Delay used for a retry attempt, honouring <c>Retry-After</c> with a configured cap.
    /// </summary>
    public static TimeSpan ResolveRetryDelay(
        FieldFlowResilienceOptions options,
        HttpResponseMessage? response,
        int attemptNumber)
    {
        if (options.DisableRetryDelays)
        {
            return TimeSpan.Zero;
        }

        if (response?.Headers.RetryAfter is { } retryAfter)
        {
            var fromHeader = retryAfter.Delta
                             ?? (retryAfter.Date is { } date
                                 ? date - DateTimeOffset.UtcNow
                                 : null);
            if (fromHeader is { } headerDelay && headerDelay > TimeSpan.Zero)
            {
                var capped = TimeSpan.FromMilliseconds(options.MaxRetryAfterMilliseconds);
                return headerDelay > capped ? capped : headerDelay;
            }
        }

        var exponential = options.BaseDelayMilliseconds * Math.Pow(2, Math.Max(0, attemptNumber - 1));
        var delayMs = Math.Min(exponential, options.MaxDelayMilliseconds);
        return TimeSpan.FromMilliseconds(delayMs);
    }

    private static HttpRetryStrategyOptions CreateRetryOptions(FieldFlowResilienceOptions options) =>
        new()
        {
            MaxRetryAttempts = options.MaxRetryAttempts,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = !options.DisableRetryDelays,
            Delay = TimeSpan.FromMilliseconds(Math.Max(0, options.BaseDelayMilliseconds)),
            MaxDelay = TimeSpan.FromMilliseconds(Math.Max(0, options.MaxDelayMilliseconds)),
            ShouldHandle = args => ValueTask.FromResult(IsTransient(args.Outcome)),
            DelayGenerator = args =>
            {
                var delay = ResolveRetryDelay(options, args.Outcome.Result, args.AttemptNumber);
                return new ValueTask<TimeSpan?>(delay);
            }
        };

    private static HttpCircuitBreakerStrategyOptions CreateCircuitOptions(
        FieldFlowResilienceOptions options,
        FieldFlowResilienceState state,
        TimeProvider timeProvider) =>
        new()
        {
            FailureRatio = options.CircuitFailureRatio,
            MinimumThroughput = options.CircuitMinimumThroughput,
            SamplingDuration = TimeSpan.FromSeconds(options.CircuitSamplingDurationSeconds),
            BreakDuration = TimeSpan.FromSeconds(options.CircuitBreakDurationSeconds),
            ShouldHandle = args => ValueTask.FromResult(IsAvailabilityFailure(args.Outcome)),
            OnOpened = _ =>
            {
                state.SetCircuitOpen(timeProvider.GetUtcNow());
                return ValueTask.CompletedTask;
            },
            OnClosed = _ =>
            {
                state.SetCircuitClosed();
                return ValueTask.CompletedTask;
            },
            OnHalfOpened = _ =>
            {
                state.SetCircuitHalfOpen();
                return ValueTask.CompletedTask;
            }
        };

    /// <summary>
    /// Transient outcomes eligible for HTTP retry (not validation/auth).
    /// </summary>
    public static bool IsTransient(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is TimeoutRejectedException or HttpRequestException)
        {
            return true;
        }

        if (outcome.Exception is OperationCanceledException)
        {
            return false;
        }

        var status = outcome.Result?.StatusCode;
        return status is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout
            or >= HttpStatusCode.InternalServerError;
    }

    /// <summary>
    /// Availability-relevant failures for the circuit breaker.
    /// Prolonged 429 counts: sustained capacity pressure is treated as provider unavailability.
    /// Validation/auth 4xx never open the circuit.
    /// </summary>
    public static bool IsAvailabilityFailure(Outcome<HttpResponseMessage> outcome) =>
        IsTransient(outcome);
}
