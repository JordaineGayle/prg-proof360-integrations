using PRG.Proof360.Integrations.Api.Errors;
using PRG.Proof360.Integrations.Application.Errors;

namespace PRG.Proof360.Integrations.IntegrationTests.Errors;

public sealed class ProblemDetailsMapperTests
{
    [Theory]
    [InlineData(FailureCategory.Validation, FailureCodes.ValidationFailed, 400)]
    [InlineData(FailureCategory.NotFound, FailureCodes.JobNotFound, 404)]
    [InlineData(FailureCategory.Conflict, FailureCodes.InvalidStatusTransition, 409)]
    [InlineData(FailureCategory.Approval, FailureCodes.VendorNotApproved, 422)]
    [InlineData(FailureCategory.ProviderAuthentication, FailureCodes.ProviderAuthenticationFailed, 503)]
    [InlineData(FailureCategory.Unauthorized, FailureCodes.WebhookSignatureInvalid, 401)]
    [InlineData(FailureCategory.RateLimited, FailureCodes.ProviderRateLimited, 503)]
    [InlineData(FailureCategory.Timeout, FailureCodes.ProviderTimeout, 504)]
    [InlineData(FailureCategory.Unavailable, FailureCodes.CircuitOpen, 503)]
    [InlineData(FailureCategory.Unexpected, FailureCodes.UnexpectedError, 500)]
    public void Maps_every_category_to_expected_status(FailureCategory category, string code, int status)
    {
        var failure = new IntegrationFailure(code, "safe message", category, RetryAfter: TimeSpan.FromSeconds(2));
        var problem = ProblemDetailsMapper.ToProblemDetails(failure, "corr-1");

        Assert.Equal(status, problem.Status);
        Assert.Equal(ProblemDetailsMapper.TypePrefix + code, problem.Type);
        Assert.Equal("safe message", problem.Detail);
        Assert.Equal(code, problem.Extensions["code"]);
        Assert.Equal("corr-1", problem.Extensions["correlationId"]);
        Assert.Equal(2, problem.Extensions["retryAfterSeconds"]);
        Assert.DoesNotContain("Exception", problem.Detail ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("stack", problem.Detail ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Does_not_embed_unsafe_provider_payloads()
    {
        var failure = new IntegrationFailure(
            FailureCodes.MalformedProviderPayload,
            "Provider payload was malformed.",
            FailureCategory.ProviderContract,
            ProviderCode: "raw_code_ok");

        var problem = ProblemDetailsMapper.ToProblemDetails(failure, "c2");
        var json = System.Text.Json.JsonSerializer.Serialize(problem);
        Assert.DoesNotContain("Authorization", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("+1-", json, StringComparison.Ordinal);
    }
}
