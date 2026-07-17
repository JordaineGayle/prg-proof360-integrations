using PRG.Proof360.Integrations.Domain;

namespace PRG.Proof360.Integrations.UnitTests.Smoke;

public sealed class DomainAssemblySmokeTests
{
    [Fact]
    public void Domain_assembly_marker_is_available()
    {
        Assert.Equal("PRG.Proof360.Integrations.Domain", AssemblyMarker.Name);
    }
}
