using PRG.Proof360.Integrations.Application.Errors;
using PRG.Proof360.Integrations.Core.Providers.Contracts;

namespace PRG.Proof360.Integrations.UnitTests.Errors;

public sealed class ProviderFailureTranslatorTests
{
    [Theory]
    [InlineData(ProviderFailureKind.Validation, "missing_work_order_id", FailureCategory.Validation, FailureCodes.RequiredFieldMissing)]
    [InlineData(ProviderFailureKind.Authentication, "unauthorized", FailureCategory.ProviderAuthentication, FailureCodes.ProviderAuthenticationFailed)]
    [InlineData(ProviderFailureKind.RateLimited, "rate_limited", FailureCategory.RateLimited, FailureCodes.ProviderRateLimited)]
    [InlineData(ProviderFailureKind.Timeout, "timeout", FailureCategory.Timeout, FailureCodes.ProviderTimeout)]
    [InlineData(ProviderFailureKind.AmbiguousWrite, "ambiguous_write", FailureCategory.Timeout, FailureCodes.AmbiguousProviderWrite)]
    [InlineData(ProviderFailureKind.ContractViolation, "unsupported_capability", FailureCategory.ProviderContract, FailureCodes.UnsupportedCapability)]
    [InlineData(ProviderFailureKind.NotFound, "not_found", FailureCategory.NotFound, FailureCodes.ProviderNotFound)]
    [InlineData(ProviderFailureKind.Conflict, "idempotency_conflict", FailureCategory.Conflict, FailureCodes.ProviderConflict)]
    public void Translates_provider_kinds(
        ProviderFailureKind kind,
        string providerCode,
        FailureCategory expectedCategory,
        string expectedCode)
    {
        var failure = new ProviderFailure(kind, providerCode, "safe", TimeSpan.FromSeconds(3), 429);
        var translated = ProviderFailureTranslator.ToIntegrationFailure(failure);

        Assert.Equal(expectedCategory, translated.Category);
        Assert.Equal(expectedCode, translated.Code);
        Assert.Equal(providerCode, translated.ProviderCode);
        Assert.Equal(TimeSpan.FromSeconds(3), translated.RetryAfter);
        Assert.Equal("safe", translated.SafeMessage);
    }
}
