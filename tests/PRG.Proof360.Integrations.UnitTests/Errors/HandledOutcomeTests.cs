using PRG.Proof360.Integrations.Application.Errors;
using PRG.Proof360.Integrations.Application.Outcomes;
using PRG.Proof360.Integrations.Core.Results;

namespace PRG.Proof360.Integrations.UnitTests.Errors;

public sealed class HandledOutcomeTests
{
    [Fact]
    public void Duplicate_stale_and_no_change_are_typed_success_outcomes()
    {
        Result<ReceiveEventOutcome, IntegrationFailure> duplicate =
            Result<ReceiveEventOutcome, IntegrationFailure>.Ok(
                new ReceiveEventOutcome.Duplicate(Guid.NewGuid()));

        Result<ApplyWorkOrderOutcome, IntegrationFailure> stale =
            Result<ApplyWorkOrderOutcome, IntegrationFailure>.Ok(
                new ApplyWorkOrderOutcome.IgnoredStale(Guid.NewGuid(), 3));

        Result<ApplyWorkOrderOutcome, IntegrationFailure> noChange =
            Result<ApplyWorkOrderOutcome, IntegrationFailure>.Ok(
                new ApplyWorkOrderOutcome.NoChange(Guid.NewGuid()));

        Assert.True(duplicate.IsSuccess);
        Assert.True(stale.IsSuccess);
        Assert.True(noChange.IsSuccess);
        Assert.IsType<ReceiveEventOutcome.Duplicate>(
            duplicate.Match(o => o, _ => throw new InvalidOperationException()));
    }
}
