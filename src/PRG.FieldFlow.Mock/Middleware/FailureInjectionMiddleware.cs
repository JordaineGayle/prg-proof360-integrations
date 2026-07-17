using PRG.FieldFlow.Mock.Models;
using PRG.FieldFlow.Mock.State;

namespace PRG.FieldFlow.Mock.Middleware;

/// <summary>
/// Applies deterministic failure injection to normal provider endpoints only.
/// </summary>
public sealed class FailureInjectionMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>Creates the middleware.</summary>
    public FailureInjectionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>Invokes the middleware.</summary>
    public async Task InvokeAsync(HttpContext context, MockStore store)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/_test", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var failure = store.Failures.ConsumeProviderFailure();
        if (failure is null)
        {
            await _next(context);
            return;
        }

        switch (failure.Kind)
        {
            case InjectedFailureKind.RateLimited:
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers.RetryAfter = failure.RetryAfterSeconds.ToString();
                await context.Response.WriteAsJsonAsync(new ErrorResponse
                {
                    Code = "rate_limited",
                    Message = "Injected 429 for resilience testing."
                });
                return;

            case InjectedFailureKind.ServerError:
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new ErrorResponse
                {
                    Code = "server_error",
                    Message = "Injected 500 for resilience testing."
                });
                return;

            case InjectedFailureKind.Timeout:
                await Task.Delay(failure.TimeoutDelayMilliseconds, context.RequestAborted);
                context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
                await context.Response.WriteAsJsonAsync(new ErrorResponse
                {
                    Code = "timeout_simulated",
                    Message = "Injected delay intended to exceed client timeouts."
                });
                return;
        }

        await _next(context);
    }
}
