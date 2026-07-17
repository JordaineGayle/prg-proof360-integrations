using Microsoft.AspNetCore.Mvc;
using PRG.Proof360.Integrations.Application.Errors;

namespace PRG.Proof360.Integrations.Api.Errors;

/// <summary>
/// Centralized RFC 7807 mapping from <see cref="IntegrationFailure"/> to Problem Details.
/// </summary>
public static class ProblemDetailsMapper
{
    /// <summary>URN type prefix for integration errors.</summary>
    public const string TypePrefix = "urn:prg:integration:error:";

    /// <summary>
    /// Maps an application failure to Problem Details. Never includes unsafe details.
    /// </summary>
    public static ProblemDetails ToProblemDetails(IntegrationFailure failure, string correlationId)
    {
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var status = MapStatus(failure);
        var problem = new ProblemDetails
        {
            Type = TypePrefix + failure.Code,
            Title = TitleFor(failure.Category),
            Detail = failure.SafeMessage,
            Status = status,
            Instance = null
        };

        problem.Extensions["code"] = failure.Code;
        problem.Extensions["correlationId"] = correlationId;
        problem.Extensions["retryable"] = IsCallerRetryable(failure);
        if (failure.RetryAfter is { } retryAfter)
        {
            problem.Extensions["retryAfterSeconds"] = (int)Math.Ceiling(retryAfter.TotalSeconds);
        }

        if (failure.ValidationErrors is { Count: > 0 })
        {
            problem.Extensions["errors"] = failure.ValidationErrors;
        }

        return problem;
    }

    /// <summary>HTTP status for a failure category/code.</summary>
    public static int MapStatus(IntegrationFailure failure) =>
        failure.Category switch
        {
            FailureCategory.Validation => 400,
            FailureCategory.NotFound => 404,
            FailureCategory.Conflict => 409,
            FailureCategory.Approval => 422,
            FailureCategory.Dependency => 422,
            FailureCategory.ProviderContract => 422,
            FailureCategory.ProviderAuthentication => 503,
            FailureCategory.Unauthorized => 401,
            FailureCategory.RateLimited => 503,
            FailureCategory.Timeout => 504,
            FailureCategory.Unavailable => 503,
            FailureCategory.PersistenceConflict => 409,
            FailureCategory.Unexpected => 500,
            _ => 500
        };

    private static string TitleFor(FailureCategory category) =>
        category switch
        {
            FailureCategory.Validation => "Validation failed",
            FailureCategory.NotFound => "Not found",
            FailureCategory.Conflict => "Conflict",
            FailureCategory.Approval => "Approval required",
            FailureCategory.Dependency => "Dependency missing",
            FailureCategory.ProviderContract => "Provider contract error",
            FailureCategory.ProviderAuthentication => "Provider authentication error",
            FailureCategory.Unauthorized => "Unauthorized",
            FailureCategory.RateLimited => "Provider rate limited",
            FailureCategory.Timeout => "Provider timeout",
            FailureCategory.Unavailable => "Provider unavailable",
            FailureCategory.PersistenceConflict => "Persistence conflict",
            _ => "Unexpected error"
        };

    private static bool IsCallerRetryable(IntegrationFailure failure) =>
        failure.Category is FailureCategory.RateLimited
            or FailureCategory.Timeout
            or FailureCategory.Unavailable
            or FailureCategory.PersistenceConflict;
}
