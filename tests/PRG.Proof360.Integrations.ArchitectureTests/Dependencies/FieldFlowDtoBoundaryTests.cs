using System.Reflection;
using PRG.Proof360.Integrations.Application.DependencyInjection;
using PRG.Proof360.Integrations.Domain;
using PRG.Proof360.Integrations.Domain.Canonical;
using PRG.Proof360.Integrations.FieldFlow.Contracts;

namespace PRG.Proof360.Integrations.ArchitectureTests.Dependencies;

public sealed class FieldFlowDtoBoundaryTests
{
    [Fact]
    public void Application_and_Domain_have_no_FieldFlow_DTO_dependency()
    {
        var domain = typeof(AssemblyMarker).Assembly;
        var application = typeof(ApplicationServiceCollectionExtensions).Assembly;
        var fieldFlowDtoAssembly = typeof(FieldFlowContractorDto).Assembly;
        _ = typeof(Vendor);

        Assert.DoesNotContain(domain.GetReferencedAssemblies(), IsFieldFlow);
        Assert.DoesNotContain(application.GetReferencedAssemblies(), IsFieldFlow);

        Assert.DoesNotContain(GetAllTypes(domain), IsFieldFlowType);
        Assert.DoesNotContain(GetAllTypes(application), IsFieldFlowType);

        // Sanity: FieldFlow assembly itself exposes the DTO type under test.
        Assert.Contains(GetAllTypes(fieldFlowDtoAssembly), static t => t == typeof(FieldFlowContractorDto));
    }

    private static bool IsFieldFlow(AssemblyName name) =>
        string.Equals(name.Name, "PRG.Proof360.Integrations.FieldFlow", StringComparison.Ordinal);

    private static bool IsFieldFlowType(Type type) =>
        type.Namespace is not null &&
        type.Namespace.StartsWith("PRG.Proof360.Integrations.FieldFlow", StringComparison.Ordinal);

    private static IEnumerable<Type> GetAllTypes(Assembly assembly) =>
        assembly.GetTypes().Where(static t => t.Namespace is not null);
}
