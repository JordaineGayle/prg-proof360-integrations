namespace PRG.Proof360.Integrations.Api.Middleware;

/// <summary>
/// Ensures every request has a correlation id for logs and Problem Details.
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
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var values) &&
                            !string.IsNullOrWhiteSpace(values.ToString())
            ? values.ToString()
            : Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        await _next(context);
    }

    /// <summary>Reads the correlation id from the current context.</summary>
    public static string GetCorrelationId(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) && value is string s && !string.IsNullOrWhiteSpace(s)
            ? s
            : "unknown";
}
