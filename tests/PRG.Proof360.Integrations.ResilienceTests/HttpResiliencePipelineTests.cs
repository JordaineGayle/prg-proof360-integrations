using System.Net;
using Microsoft.Extensions.Time.Testing;
using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.Core.Results;
using PRG.Proof360.Integrations.FieldFlow.Resilience;
using PRG.Proof360.Integrations.ResilienceTests.Support;

namespace PRG.Proof360.Integrations.ResilienceTests;

public sealed class HttpResiliencePipelineTests
{
    [Fact]
    public async Task Transient_500_then_success_uses_bounded_attempts()
    {
        await using var fx = ResilienceTestFixture.Create(o => o.MaxRetryAttempts = 3);
        fx.Handler
            .EnqueueStatus(HttpStatusCode.InternalServerError)
            .EnqueueStatus(HttpStatusCode.InternalServerError)
            .EnqueueSuccessContractors();

        var result = await fx.Client.ListContractorsAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, fx.Handler.AttemptCount);
        Assert.Equal(3, fx.State.HttpAttemptCount);
    }

    [Fact]
    public async Task Timeout_retries_are_bounded()
    {
        await using var fx = ResilienceTestFixture.Create(o =>
        {
            o.MaxRetryAttempts = 2;
            o.AttemptTimeoutMilliseconds = 50;
            o.DisableRetryDelays = true;
        });
        fx.Handler
            .EnqueueHangUntilCanceled()
            .EnqueueHangUntilCanceled()
            .EnqueueHangUntilCanceled();

        var task = fx.Client.ListContractorsAsync(CancellationToken.None);
        await AdvanceUntilCompletedAsync(fx.Time, task, TimeSpan.FromMilliseconds(50), steps: 20);

        var result = await task;
        Assert.True(result.IsFailure);
        Assert.Equal(ProviderFailureKind.Timeout, ((Result<IReadOnlyList<ContractorSnapshot>, ProviderFailure>.Failed)result).Error.Kind);
        Assert.Equal(3, fx.Handler.AttemptCount);
        Assert.Equal(fx.Options.MaxAttemptsPerHttpCall, fx.Handler.AttemptCount);
    }

    [Fact]
    public async Task Rate_limit_honours_Retry_After_without_real_sleep()
    {
        await using var fx = ResilienceTestFixture.Create(o =>
        {
            o.MaxRetryAttempts = 1;
            o.DisableRetryDelays = false;
            o.BaseDelayMilliseconds = 5_000;
            o.MaxRetryAfterMilliseconds = 120_000;
        });
        fx.Handler
            .EnqueueStatus(HttpStatusCode.TooManyRequests, retryAfterSeconds: 45)
            .EnqueueSuccessContractors();

        var delay = FieldFlowResiliencePipeline.ResolveRetryDelay(
            fx.Options,
            new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Headers = { RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(45)) }
            },
            attemptNumber: 1);
        Assert.Equal(TimeSpan.FromSeconds(45), delay);

        var task = fx.Client.ListContractorsAsync(CancellationToken.None);
        // Bounded probe: let Polly schedule the Retry-After delay on FakeTimeProvider.
        await Task.Delay(15);
        Assert.False(task.IsCompleted);

        fx.Time.Advance(TimeSpan.FromSeconds(45));
        await AdvanceUntilCompletedAsync(fx.Time, task, TimeSpan.FromSeconds(1), steps: 50);

        var result = await task;
        Assert.True(result.IsSuccess);
        Assert.Equal(2, fx.Handler.AttemptCount);
    }

    [Fact]
    public async Task Validation_400_is_called_once()
    {
        await using var fx = ResilienceTestFixture.Create();
        fx.Handler.EnqueueStatus(HttpStatusCode.BadRequest, """{"code":"invalid","message":"bad"}""");

        var result = await fx.Client.ListContractorsAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ProviderFailureKind.Validation, ((Result<IReadOnlyList<ContractorSnapshot>, ProviderFailure>.Failed)result).Error.Kind);
        Assert.Equal(1, fx.Handler.AttemptCount);
    }

    [Fact]
    public async Task Authentication_401_is_called_once_and_sets_NeedsAttention()
    {
        await using var fx = ResilienceTestFixture.Create();
        fx.Handler.EnqueueStatus(HttpStatusCode.Unauthorized, """{"code":"unauthorized","message":"auth"}""");

        var result = await fx.Client.ListContractorsAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ProviderFailureKind.Authentication, ((Result<IReadOnlyList<ContractorSnapshot>, ProviderFailure>.Failed)result).Error.Kind);
        Assert.Equal(1, fx.Handler.AttemptCount);
        Assert.True(fx.State.NeedsAttention);
    }

    [Fact]
    public async Task Failure_threshold_opens_circuit()
    {
        await using var fx = ResilienceTestFixture.Create(o =>
        {
            o.MaxRetryAttempts = 0;
            o.CircuitMinimumThroughput = 2;
            o.CircuitFailureRatio = 1.0;
        });
        fx.Handler
            .EnqueueStatus(HttpStatusCode.InternalServerError)
            .EnqueueStatus(HttpStatusCode.InternalServerError);

        _ = await fx.Client.ListContractorsAsync(CancellationToken.None);
        _ = await fx.Client.ListContractorsAsync(CancellationToken.None);

        Assert.Equal(FieldFlowCircuitStates.Open, fx.State.CircuitState);
    }

    [Fact]
    public async Task Open_circuit_does_not_reach_transport()
    {
        await using var fx = ResilienceTestFixture.Create(o =>
        {
            o.MaxRetryAttempts = 0;
            o.CircuitMinimumThroughput = 2;
            o.CircuitFailureRatio = 1.0;
        });
        fx.Handler
            .EnqueueStatus(HttpStatusCode.InternalServerError)
            .EnqueueStatus(HttpStatusCode.InternalServerError)
            .EnqueueSuccessContractors();

        _ = await fx.Client.ListContractorsAsync(CancellationToken.None);
        _ = await fx.Client.ListContractorsAsync(CancellationToken.None);
        var before = fx.Handler.AttemptCount;

        var result = await fx.Client.ListContractorsAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ProviderFailureKind.CircuitOpen, ((Result<IReadOnlyList<ContractorSnapshot>, ProviderFailure>.Failed)result).Error.Kind);
        Assert.Equal(before, fx.Handler.AttemptCount);
    }

    [Fact]
    public async Task Half_open_probe_success_closes_circuit()
    {
        await using var fx = ResilienceTestFixture.Create(o =>
        {
            o.MaxRetryAttempts = 0;
            o.CircuitMinimumThroughput = 2;
            o.CircuitFailureRatio = 1.0;
            o.CircuitBreakDurationSeconds = 10;
        });
        fx.Handler
            .EnqueueStatus(HttpStatusCode.InternalServerError)
            .EnqueueStatus(HttpStatusCode.InternalServerError)
            .EnqueueSuccessContractors();

        _ = await fx.Client.ListContractorsAsync(CancellationToken.None);
        _ = await fx.Client.ListContractorsAsync(CancellationToken.None);
        Assert.Equal(FieldFlowCircuitStates.Open, fx.State.CircuitState);

        fx.Time.Advance(TimeSpan.FromSeconds(11));
        var result = await fx.Client.ListContractorsAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(FieldFlowCircuitStates.Closed, fx.State.CircuitState);
    }

    [Fact]
    public async Task Failed_half_open_probe_reopens_circuit()
    {
        await using var fx = ResilienceTestFixture.Create(o =>
        {
            o.MaxRetryAttempts = 0;
            o.CircuitMinimumThroughput = 2;
            o.CircuitFailureRatio = 1.0;
            o.CircuitBreakDurationSeconds = 10;
        });
        fx.Handler
            .EnqueueStatus(HttpStatusCode.InternalServerError)
            .EnqueueStatus(HttpStatusCode.InternalServerError)
            .EnqueueStatus(HttpStatusCode.InternalServerError);

        _ = await fx.Client.ListContractorsAsync(CancellationToken.None);
        _ = await fx.Client.ListContractorsAsync(CancellationToken.None);
        fx.Time.Advance(TimeSpan.FromSeconds(11));

        var result = await fx.Client.ListContractorsAsync(CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(FieldFlowCircuitStates.Open, fx.State.CircuitState);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_retried()
    {
        await using var fx = ResilienceTestFixture.Create(o => o.MaxRetryAttempts = 3);
        fx.Handler.EnqueueHangUntilCanceled();

        using var cts = new CancellationTokenSource();
        var task = fx.Client.ListContractorsAsync(cts.Token);
        await WaitUntilAsync(() => fx.Handler.AttemptCount >= 1, timeout: TimeSpan.FromSeconds(2));
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);
        Assert.Equal(1, fx.Handler.AttemptCount);
    }

    [Fact]
    public void Maximum_nested_attempt_count_matches_retry_budget()
    {
        var options = new FieldFlowResilienceOptions { MaxRetryAttempts = 3 };
        Assert.Equal(4, options.MaxAttemptsPerHttpCall);
    }

    private static async Task AdvanceUntilCompletedAsync(
        FakeTimeProvider time,
        Task task,
        TimeSpan step,
        int steps)
    {
        for (var i = 0; i < steps && !task.IsCompleted; i++)
        {
            time.Advance(step);
            // Short wall-clock yield so FakeTimeProvider timer callbacks can run.
            await Task.Delay(5);
        }

        Assert.True(task.IsCompleted, "Resilience call did not complete within the bounded FakeTimeProvider probe.");
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        while (!predicate())
        {
            if (DateTime.UtcNow - start > timeout)
            {
                Assert.Fail("Condition was not met within the bounded async probe.");
            }

            await Task.Delay(5);
        }
    }
}
