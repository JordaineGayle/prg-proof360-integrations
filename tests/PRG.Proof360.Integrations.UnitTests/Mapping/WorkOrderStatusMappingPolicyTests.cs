using PRG.Proof360.Integrations.Application.Mapping;
using PRG.Proof360.Integrations.Domain.Canonical;

namespace PRG.Proof360.Integrations.UnitTests.Mapping;

public sealed class WorkOrderStatusMappingPolicyTests
{
    private readonly WorkOrderStatusMappingPolicy _policy = new();

    [Theory]
    [InlineData("open", JobStatuses.Dispatched)]
    [InlineData("scheduled", JobStatuses.Scheduled)]
    [InlineData("in_progress", JobStatuses.InProgress)]
    [InlineData("done", JobStatuses.Completed)]
    [InlineData("void", JobStatuses.Cancelled)]
    public void Maps_every_fieldflow_status(string provider, string expected)
    {
        var result = _policy.MapFieldFlow(provider);
        Assert.True(result.IsValid);
        Assert.Equal(expected, result.JobStatus);
    }

    [Fact]
    public void Unknown_status_fails_stably()
    {
        var result = _policy.MapFieldFlow("mystery");
        Assert.False(result.IsValid);
        Assert.Contains("Unknown FieldFlow status", result.Reason!, StringComparison.Ordinal);
    }
}
