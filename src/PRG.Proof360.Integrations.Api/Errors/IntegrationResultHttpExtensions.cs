using PRG.Proof360.Integrations.Api.Middleware;
using PRG.Proof360.Integrations.Application.Errors;
using PRG.Proof360.Integrations.Core.Results;

namespace PRG.Proof360.Integrations.Api.Errors;

/// <summary>
/// Maps application <see cref="Result{TSuccess,TFailure}"/> values to HTTP results.
/// </summary>
public static class IntegrationResultHttpExtensions
{
    /// <summary>
    /// Converts a result to an <see cref="IResult"/> using Problem Details on failure.
    /// </summary>
    public static IResult ToHttpResult<TSuccess>(
        this Result<TSuccess, IntegrationFailure> result,
        HttpContext httpContext,
        Func<TSuccess, IResult> onSuccess)
        where TSuccess : notnull
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(onSuccess);

        return result.Match(
            onSuccess,
            failure =>
            {
                var correlationId = CorrelationIdMiddleware.GetCorrelationId(httpContext);
                var problem = ProblemDetailsMapper.ToProblemDetails(failure, correlationId);
                return Results.Json(problem, statusCode: problem.Status ?? 500, contentType: "application/problem+json");
            });
    }
}
