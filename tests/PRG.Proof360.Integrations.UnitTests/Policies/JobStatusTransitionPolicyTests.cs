using PRG.Proof360.Integrations.Domain.Canonical;
using PRG.Proof360.Integrations.Domain.Policies;

namespace PRG.Proof360.Integrations.UnitTests.Policies;

public sealed class JobStatusTransitionPolicyTests
{
    private readonly JobStatusTransitionPolicy _policy = new();

    [Theory]
    [InlineData(JobStatuses.Qualified, JobStatuses.Dispatched)]
    [InlineData(JobStatuses.Dispatched, JobStatuses.Scheduled)]
    [InlineData(JobStatuses.Scheduled, JobStatuses.InProgress)]
    [InlineData(JobStatuses.InProgress, JobStatuses.Completed)]
    [InlineData(JobStatuses.Dispatched, JobStatuses.Cancelled)]
    public void Allowed_transitions_succeed(string from, string to)
    {
        var result = _policy.Evaluate(from, to);
        Assert.True(result.IsAllowed);
        Assert.False(result.IsNoOp);
    }

    [Fact]
    public void Equal_status_is_noop()
    {
        var result = _policy.Evaluate(JobStatuses.Scheduled, JobStatuses.Scheduled);
        Assert.True(result.IsNoOp);
        Assert.False(result.IsAllowed);
    }

    [Theory]
    [InlineData(JobStatuses.Completed, JobStatuses.InProgress)]
    [InlineData(JobStatuses.Cancelled, JobStatuses.Dispatched)]
    [InlineData(JobStatuses.InProgress, JobStatuses.Qualified)]
    [InlineData(JobStatuses.Scheduled, JobStatuses.Dispatched)]
    public void Invalid_or_regressive_transitions_are_rejected(string from, string to)
    {
        var result = _policy.Evaluate(from, to);
        Assert.False(result.IsAllowed);
        Assert.False(result.IsNoOp);
    }

    [Fact]
    public void Cancellation_from_non_terminal_is_allowed()
    {
        Assert.True(_policy.Evaluate(JobStatuses.InProgress, JobStatuses.Cancelled).IsAllowed);
    }
}
