using PRG.Proof360.Integrations.Application.Observability;

namespace PRG.Proof360.Integrations.Api.Middleware;

/// <summary>
/// Ensures every request has a validated correlation id for logs, audit, and Problem Details.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    /// <summary>Header name.</summary>
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>HttpContext item key.</summary>
    public const string ItemKey = "CorrelationId";

    private readonly RequestDelegate _next;

    /// <summary>Creates the middleware.</summary>
    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    /// <summary>Invokes the middleware.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        context.Request.Headers.TryGetValue(HeaderName, out var values);
        var correlationId = CorrelationIdRules.Resolve(values.ToString());

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        await _next(context);
    }

    /// <summary>Reads the correlation id from the current context.</summary>
    public static string GetCorrelationId(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) && value is string s && !string.IsNullOrWhiteSpace(s)
            ? s
            : CorrelationIdRules.NewId();
}
