using PRG.Proof360.Integrations.Application.Observability;

namespace PRG.Proof360.Integrations.UnitTests.Observability;

public sealed class CorrelationIdRulesTests
{
    [Fact]
    public void Missing_correlation_is_generated()
    {
        var id = CorrelationIdRules.Resolve(null);
        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.True(CorrelationIdRules.IsValid(id));
    }

    [Fact]
    public void Valid_correlation_is_accepted()
    {
        Assert.Equal("abc-123_ops.1", CorrelationIdRules.Resolve("abc-123_ops.1"));
    }

    [Theory]
    [InlineData("has spaces")]
    [InlineData("bad!chars")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public void Invalid_or_oversized_correlation_is_replaced(string candidate)
    {
        var id = CorrelationIdRules.Resolve(candidate);
        Assert.NotEqual(candidate.Trim(), id);
        Assert.True(CorrelationIdRules.IsValid(id));
    }
}
