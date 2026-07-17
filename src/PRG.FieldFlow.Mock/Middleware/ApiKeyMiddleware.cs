using Microsoft.Extensions.Options;
using PRG.FieldFlow.Mock.Models;
using PRG.FieldFlow.Mock.Options;

namespace PRG.FieldFlow.Mock.Middleware;

/// <summary>
/// Validates <c>X-Api-Key</c> for provider endpoints. Does not log the key value.
/// </summary>
public sealed class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>Creates the middleware.</summary>
    public ApiKeyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>Invokes the middleware.</summary>
    public async Task InvokeAsync(HttpContext context, IOptions<FieldFlowMockOptions> options)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/_test", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var expected = options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(expected))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new ErrorResponse
            {
                Code = "mock_misconfigured",
                Message = "API key is not configured for the mock."
            });
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-Api-Key", out var provided) ||
            !FixedTimeEquals(provided.ToString(), expected))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new ErrorResponse
            {
                Code = "unauthorized",
                Message = "Missing or invalid API key."
            });
            return;
        }

        await _next(context);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var a = System.Text.Encoding.UTF8.GetBytes(left);
        var b = System.Text.Encoding.UTF8.GetBytes(right);
        if (a.Length != b.Length)
        {
            return false;
        }

        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, b);
    }
}
