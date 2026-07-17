using System.Net;
using System.Text;

namespace PRG.Proof360.Integrations.ResilienceTests.Support;

/// <summary>
/// Deterministic HTTP handler for resilience tests. Counts every attempt that reaches transport.
/// </summary>
public sealed class ScriptedHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _scripts = new();
    private int _attemptCount;

    /// <summary>Number of attempts that reached this handler.</summary>
    public int AttemptCount => _attemptCount;

    /// <summary>Enqueues a fixed status response.</summary>
    public ScriptedHttpMessageHandler EnqueueStatus(HttpStatusCode status, string? body = null, int? retryAfterSeconds = null)
    {
        _scripts.Enqueue((_, _) =>
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(body ?? """{"code":"test","message":"scripted"}""", Encoding.UTF8, "application/json")
            };
            if (retryAfterSeconds is not null)
            {
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
                    TimeSpan.FromSeconds(retryAfterSeconds.Value));
            }

            return Task.FromResult(response);
        });
        return this;
    }

    /// <summary>Enqueues a hang that only completes when the attempt token is canceled (timeout).</summary>
    public ScriptedHttpMessageHandler EnqueueHangUntilCanceled()
    {
        _scripts.Enqueue(async (_, cancellationToken) =>
        {
            var tcs = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var reg = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
            return await tcs.Task;
        });
        return this;
    }

    /// <summary>Enqueues a successful empty contractor list.</summary>
    public ScriptedHttpMessageHandler EnqueueSuccessContractors()
    {
        _scripts.Enqueue((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json")
        }));
        return this;
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _attemptCount);
        if (_scripts.Count == 0)
        {
            throw new InvalidOperationException("No scripted HTTP responses remaining.");
        }

        return _scripts.Dequeue()(request, cancellationToken);
    }
}
