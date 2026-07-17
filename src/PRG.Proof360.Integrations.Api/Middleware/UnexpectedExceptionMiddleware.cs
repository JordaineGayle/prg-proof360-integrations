using System.Text.Json;
using PRG.Proof360.Integrations.Api.Errors;
using PRG.Proof360.Integrations.Application.Errors;

namespace PRG.Proof360.Integrations.Api.Middleware;

/// <summary>
/// Outer exception boundary: logs unexpected exceptions once with correlation id and returns sanitized 500.
/// Does not convert client cancellation into application failures.
/// </summary>
public sealed class UnexpectedExceptionMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<UnexpectedExceptionMiddleware> _logger;

    /// <summary>Creates the middleware.</summary>
    public UnexpectedExceptionMiddleware(RequestDelegate next, ILogger<UnexpectedExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>Invokes the middleware.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnect — do not log as application failure.
        }
        catch (Exception ex)
        {
            var correlationId = CorrelationIdMiddleware.GetCorrelationId(context);
            _logger.LogError(
                ex,
                "Unexpected exception. CorrelationId={CorrelationId}",
                correlationId);

            if (context.Response.HasStarted)
            {
                throw;
            }

            var failure = IntegrationFailure.Unexpected();
            var problem = ProblemDetailsMapper.ToProblemDetails(failure, correlationId);
            context.Response.Clear();
            context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
        }
    }
}
