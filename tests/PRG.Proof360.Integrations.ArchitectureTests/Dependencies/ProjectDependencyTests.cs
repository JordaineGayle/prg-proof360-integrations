using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace PRG.Proof360.Integrations.ArchitectureTests.Dependencies;

public sealed class ProjectDependencyTests
{
    [Fact]
    public void Domain_has_no_project_references()
    {
        Assert.Empty(GetProjectReferences("src/PRG.Proof360.Integrations.Domain/PRG.Proof360.Integrations.Domain.csproj"));
    }

    [Fact]
    public void Core_only_references_Domain()
    {
        var references = GetProjectReferences("src/PRG.Proof360.Integrations.Core/PRG.Proof360.Integrations.Core.csproj");
        Assert.Equal(["PRG.Proof360.Integrations.Domain"], references);
    }

    [Fact]
    public void Application_references_only_Domain_and_Core()
    {
        var references = GetProjectReferences("src/PRG.Proof360.Integrations.Application/PRG.Proof360.Integrations.Application.csproj");
        Assert.Equal(
            ["PRG.Proof360.Integrations.Core", "PRG.Proof360.Integrations.Domain"],
            references);
    }

    [Fact]
    public void FieldFlow_does_not_reference_Application_Infrastructure_or_Api()
    {
        var references = GetProjectReferences("src/PRG.Proof360.Integrations.FieldFlow/PRG.Proof360.Integrations.FieldFlow.csproj");
        Assert.DoesNotContain("PRG.Proof360.Integrations.Application", references);
        Assert.DoesNotContain("PRG.Proof360.Integrations.Infrastructure", references);
        Assert.DoesNotContain("PRG.Proof360.Integrations.Api", references);
        Assert.DoesNotContain("PRG.FieldFlow.Mock", references);
    }

    [Fact]
    public void Infrastructure_does_not_reference_FieldFlow_or_Api()
    {
        var references = GetProjectReferences("src/PRG.Proof360.Integrations.Infrastructure/PRG.Proof360.Integrations.Infrastructure.csproj");
        Assert.DoesNotContain("PRG.Proof360.Integrations.FieldFlow", references);
        Assert.DoesNotContain("PRG.Proof360.Integrations.Api", references);
        Assert.Contains("PRG.Proof360.Integrations.Application", references);
    }

    [Fact]
    public void Api_is_composition_root_with_expected_references()
    {
        var references = GetProjectReferences("src/PRG.Proof360.Integrations.Api/PRG.Proof360.Integrations.Api.csproj");
        Assert.Contains("PRG.Proof360.Integrations.Application", references);
        Assert.Contains("PRG.Proof360.Integrations.Infrastructure", references);
        Assert.Contains("PRG.Proof360.Integrations.FieldFlow", references);
        Assert.DoesNotContain("PRG.FieldFlow.Mock", references);
    }

    [Fact]
    public void FieldFlow_Mock_has_no_connector_project_references()
    {
        var references = GetProjectReferences("src/PRG.FieldFlow.Mock/PRG.FieldFlow.Mock.csproj");
        Assert.Empty(references);
    }

    private static IReadOnlyList<string> GetProjectReferences(
        string relativeProjectPath,
        [CallerFilePath] string? callerFilePath = null)
    {
        var repoRoot = FindRepoRoot(callerFilePath);
        var projectPath = Path.Combine(repoRoot, relativeProjectPath);
        var document = XDocument.Load(projectPath);
        return document
            .Descendants("ProjectReference")
            .Select(static element => element.Attribute("Include")?.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => Path.GetFileNameWithoutExtension(value!.Replace('\\', Path.DirectorySeparatorChar)))
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
    }

    private static string FindRepoRoot(string? callerFilePath)
    {
        var start = !string.IsNullOrWhiteSpace(callerFilePath)
            ? new DirectoryInfo(Path.GetDirectoryName(callerFilePath)!)
            : new DirectoryInfo(AppContext.BaseDirectory);

        var directory = start;
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PRG.Proof360.Integrations.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the solution root starting from '{start.FullName}'.");
    }
}
