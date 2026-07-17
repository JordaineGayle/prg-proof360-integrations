using PRG.Proof360.Integrations.Core.Results;

namespace PRG.Proof360.Integrations.UnitTests.Results;

public sealed class ResultTests
{
    [Fact]
    public void Ok_and_Fail_guard_null_payloads()
    {
        Assert.Throws<ArgumentNullException>(() => Result<string, string>.Ok(null!));
        Assert.Throws<ArgumentNullException>(() => Result<string, string>.Fail(null!));
    }

    [Fact]
    public void Match_Map_and_Bind_preserve_success_and_failure()
    {
        var ok = Result<int, string>.Ok(2);
        var mapped = ok.Map(static x => x * 3);
        var bound = mapped.Bind(static x => Result<string, string>.Ok($"n={x}"));

        Assert.Equal("n=6", bound.Match(s => s, _ => "fail"));

        var fail = Result<int, string>.Fail("boom");
        Assert.Equal("boom", fail.Map(static x => x + 1).Match(_ => "ok", e => e));
        Assert.Equal("boom", fail.Bind(static x => Result<int, string>.Ok(x)).Match(_ => "ok", e => e));
    }

    [Fact]
    public void Unit_instance_is_usable_as_success_payload()
    {
        var result = Result<Unit, string>.Ok(Unit.Instance);
        Assert.True(result.IsSuccess);
        Assert.Same(Unit.Instance, result.Match(u => u, _ => throw new InvalidOperationException()));
    }
}
