using PRG.Proof360.Integrations.Core.Observability;

namespace PRG.Proof360.Integrations.UnitTests.Observability;

public sealed class ConnectorTelemetryTests
{
    [Theory]
    [InlineData("success")]
    [InlineData("dead_lettered")]
    [InlineData("rate_limited")]
    public void SanitizeLabel_keeps_low_cardinality_outcomes(string label)
    {
        Assert.Equal(label, ConnectorTelemetry.SanitizeLabel(label));
        Assert.Contains(label, ConnectorTelemetry.AllowedOutcomeLabels);
    }

    [Theory]
    [InlineData("email@x.com")]
    [InlineData("has space")]
    [InlineData("this_label_is_way_too_long_for_metrics_cardinality_guard")]
    public void SanitizeLabel_rejects_high_cardinality_or_unsafe_values(string label)
    {
        Assert.Equal("invalid", ConnectorTelemetry.SanitizeLabel(label));
    }
}
