using System.Net;
using PRG.Proof360.Integrations.Core.Providers.Contracts;
using PRG.Proof360.Integrations.FieldFlow;

namespace PRG.Proof360.Integrations.UnitTests.FieldFlow;

public sealed class FieldFlowErrorClassifierTests
{
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ProviderFailureKind.Authentication)]
    [InlineData(HttpStatusCode.TooManyRequests, ProviderFailureKind.RateLimited)]
    [InlineData(HttpStatusCode.Conflict, ProviderFailureKind.Conflict)]
    [InlineData(HttpStatusCode.GatewayTimeout, ProviderFailureKind.Timeout)]
    [InlineData(HttpStatusCode.ServiceUnavailable, ProviderFailureKind.Unavailable)]
    [InlineData(HttpStatusCode.BadRequest, ProviderFailureKind.Validation)]
    public void Expected_http_outcomes_become_provider_failures(HttpStatusCode status, ProviderFailureKind kind)
    {
        var body = """{"code":"demo","message":"safe"}""";
        var failure = FieldFlowErrorClassifier.Classify(status, body, TimeSpan.FromSeconds(1));
        Assert.Equal(kind, failure.Kind);
        Assert.Equal("demo", failure.Code);
        Assert.Equal("safe", failure.SafeMessage);
        Assert.DoesNotContain("stack", failure.SafeMessage, StringComparison.OrdinalIgnoreCase);
    }
}
