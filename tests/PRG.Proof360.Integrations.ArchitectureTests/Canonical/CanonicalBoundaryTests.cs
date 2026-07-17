using System.Reflection;
using PRG.Proof360.Integrations.Domain.Canonical;

namespace PRG.Proof360.Integrations.ArchitectureTests.Canonical;

public sealed class CanonicalBoundaryTests
{
    [Fact]
    public void Domain_assembly_does_not_reference_infrastructure_or_provider_stacks()
    {
        var references = typeof(Vendor).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", references);
        Assert.DoesNotContain("Microsoft.AspNetCore", references);
        Assert.DoesNotContain("PRG.Proof360.Integrations.FieldFlow", references);
        Assert.DoesNotContain("PRG.Proof360.Integrations.Infrastructure", references);
        Assert.DoesNotContain("PRG.Proof360.Integrations.Core", references);
    }

    [Fact]
    public void Canonical_types_do_not_expose_integration_metadata_types()
    {
        foreach (var type in new[] { typeof(Vendor), typeof(Job), typeof(Transcript) })
        {
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var propertyTypeName = property.PropertyType.FullName ?? property.PropertyType.Name;
                Assert.DoesNotContain("Integration", propertyTypeName, StringComparison.Ordinal);
                Assert.DoesNotContain("FieldFlow", propertyTypeName, StringComparison.Ordinal);
                Assert.DoesNotContain("EntityFramework", propertyTypeName, StringComparison.Ordinal);
            }
        }
    }
}
