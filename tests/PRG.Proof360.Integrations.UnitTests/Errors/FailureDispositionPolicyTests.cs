using PRG.Proof360.Integrations.Application.Errors;

namespace PRG.Proof360.Integrations.UnitTests.Errors;

public sealed class FailureDispositionPolicyTests
{
    private readonly FailureDispositionPolicy _policy = new();
    private readonly DateTimeOffset _now = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(FailureCategory.Validation, FailureCodes.ValidationFailed, typeof(FailureDisposition.DeadLetter))]
    [InlineData(FailureCategory.ProviderContract, FailureCodes.UnsupportedSchema, typeof(FailureDisposition.DeadLetter))]
    [InlineData(FailureCategory.Approval, FailureCodes.VendorNotApproved, typeof(FailureDisposition.DeadLetter))]
    [InlineData(FailureCategory.ProviderAuthentication, FailureCodes.ProviderAuthenticationFailed, typeof(FailureDisposition.NeedsAttention))]
    [InlineData(FailureCategory.Dependency, FailureCodes.ContractorMappingMissing, typeof(FailureDisposition.WaitForDependency))]
    [InlineData(FailureCategory.RateLimited, FailureCodes.ProviderRateLimited, typeof(FailureDisposition.RetryAt))]
    [InlineData(FailureCategory.Timeout, FailureCodes.ProviderTimeout, typeof(FailureDisposition.RetryAt))]
    [InlineData(FailureCategory.Unavailable, FailureCodes.ProviderUnavailable, typeof(FailureDisposition.RetryAt))]
    [InlineData(FailureCategory.PersistenceConflict, FailureCodes.WorkerClaimConflict, typeof(FailureDisposition.RetryAt))]
    [InlineData(FailureCategory.Conflict, FailureCodes.InvalidStatusTransition, typeof(FailureDisposition.DeadLetter))]
    [InlineData(FailureCategory.Conflict, FailureCodes.ConcurrencyConflict, typeof(FailureDisposition.RetryAt))]
    [InlineData(FailureCategory.Unexpected, FailureCodes.UnexpectedError, typeof(FailureDisposition.RetryAt))]
    [InlineData(FailureCategory.NotFound, FailureCodes.JobNotFound, typeof(FailureDisposition.RetryAt))]
    public void Maps_each_category_to_expected_disposition(FailureCategory category, string code, Type expectedType)
    {
        var failure = new IntegrationFailure(code, "safe", category, RetryAfter: TimeSpan.FromSeconds(5));
        var context = FreshContext(attemptCount: 1);
        var disposition = _policy.Decide(failure, context);
        Assert.IsType(expectedType, disposition);
    }

    [Fact]
    public void Exhausted_attempts_dead_letter_retryable_failures()
    {
        var failure = new IntegrationFailure(
            FailureCodes.ProviderTimeout,
            "timed out",
            FailureCategory.Timeout);
        var context = FreshContext(attemptCount: 8);
        var disposition = _policy.Decide(failure, context);
        Assert.IsType<FailureDisposition.DeadLetter>(disposition);
    }

    [Fact]
    public void Exhausted_dependency_wait_dead_letters_unknown_contractor()
    {
        var failure = new IntegrationFailure(
            FailureCodes.ContractorMappingMissing,
            "contractor not linked yet",
            FailureCategory.Dependency);
        var context = FreshContext(attemptCount: 8);
        var disposition = _policy.Decide(failure, context);
        var dead = Assert.IsType<FailureDisposition.DeadLetter>(disposition);
        Assert.Equal(FailureCodes.UnexpectedError, dead.ReasonCode);
    }

    [Fact]
    public void Exhausted_age_dead_letters_dependency_wait()
    {
        var failure = new IntegrationFailure(
            FailureCodes.ContractorMappingMissing,
            "contractor not linked yet",
            FailureCategory.NotFound);
        var context = new FailureDispositionContext(
            AttemptCount: 2,
            FirstSeenAt: _now.AddHours(-25),
            UtcNow: _now,
            MaxAttempts: 8);
        var disposition = _policy.Decide(failure, context);
        Assert.IsType<FailureDisposition.DeadLetter>(disposition);
    }

    [Fact]
    public void Rate_limited_uses_retry_after_when_present()
    {
        var failure = new IntegrationFailure(
            FailureCodes.ProviderRateLimited,
            "slow down",
            FailureCategory.RateLimited,
            RetryAfter: TimeSpan.FromSeconds(12));
        var disposition = _policy.Decide(failure, FreshContext(1));
        var retry = Assert.IsType<FailureDisposition.RetryAt>(disposition);
        Assert.Equal(_now.AddSeconds(12), retry.At);
    }

    private FailureDispositionContext FreshContext(int attemptCount) =>
        new(attemptCount, _now.AddMinutes(-1), _now);
}
