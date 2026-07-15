using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.ArchitectureTests;

/// <summary>
/// Enforces ARCHITECTURE.md "Server bounded contexts" and Dependency rule 9:
/// cross-context coupling is denied by default; only the minimal shared kernel
/// is mutually visible (issue 001, AC-02–AC-05). Fail closed: any violation is
/// a red build, no warn-only mode.
/// </summary>
public sealed class ModuleBoundaryTests
{
    [Fact]
    [Requirement("CC-MKT-001")]
    public void Solution_contains_exactly_one_module_per_bounded_context()
    {
        var moduleProjects = SolutionModel.AllProjects
            .Where(p => p.Name.StartsWith("CacheCow.Modules.", StringComparison.Ordinal))
            .Select(p => p.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var expected = SolutionModel.BoundedContexts
            .Select(SolutionModel.ModuleProjectName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, moduleProjects);
    }

    [Fact]
    public void No_module_references_another_module()
    {
        foreach (var context in SolutionModel.BoundedContexts)
        {
            var module = SolutionModel.Project(SolutionModel.ModuleProjectName(context));
            var forbidden = module.ProjectReferences
                .Where(r => !string.Equals(r, "CacheCow.SharedKernel", StringComparison.Ordinal))
                .ToArray();

            Assert.True(
                forbidden.Length == 0,
                $"{module.Name} references [{string.Join(", ", forbidden)}]; modules may reference only the shared kernel (ARCHITECTURE.md, Dependency rule 9).");
        }
    }

    [Fact]
    public void SharedKernel_references_no_project()
    {
        var kernel = SolutionModel.Project("CacheCow.SharedKernel");
        Assert.Empty(kernel.ProjectReferences);
    }

    [Fact]
    public void Modules_are_referenced_only_by_the_host_and_their_own_tests()
    {
        foreach (var context in SolutionModel.BoundedContexts)
        {
            var moduleName = SolutionModel.ModuleProjectName(context);
            var offenders = SolutionModel.AllProjects
                .Where(p => p.ProjectReferences.Contains(moduleName, StringComparer.Ordinal))
                .Where(p => p.Name != "CacheCow.Host" && p.Name != $"{moduleName}.Tests")
                .Select(p => p.Name)
                .ToArray();

            Assert.True(
                offenders.Length == 0,
                $"{moduleName} is referenced by [{string.Join(", ", offenders)}]; only the host and its own test project may reference a module (issue 001, AC-03).");
        }
    }

    [Fact]
    public void Host_references_every_bounded_context_module()
    {
        var host = SolutionModel.Project("CacheCow.Host");
        foreach (var context in SolutionModel.BoundedContexts)
        {
            Assert.Contains(SolutionModel.ModuleProjectName(context), host.ProjectReferences);
        }
    }

    [Fact]
    public void Host_is_the_only_web_entry_point()
    {
        var webProjects = SolutionModel.AllProjects
            .Where(p => File.ReadAllText(p.Path).Contains("Microsoft.NET.Sdk.Web", StringComparison.Ordinal))
            .Select(p => p.Name)
            .ToArray();

        Assert.Equal(["CacheCow.Host"], webProjects);
    }

    [Fact]
    public void No_server_project_lives_under_clients_and_no_client_code_lives_in_server_modules()
    {
        var clientsDir = Path.Combine(SolutionModel.RepoRoot, "clients");
        var serverProjectsUnderClients = SolutionModel.AllProjects
            .Where(p => p.Path.StartsWith(clientsDir, StringComparison.Ordinal))
            .ToArray();
        Assert.Empty(serverProjectsUnderClients);

        var srcDir = Path.Combine(SolutionModel.RepoRoot, "src");
        var angularArtifactsUnderSrc = Directory.Exists(srcDir)
            ? Directory.EnumerateFiles(srcDir, "angular.json", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(srcDir, "package.json", SearchOption.AllDirectories))
                .ToArray()
            : [];
        Assert.Empty(angularArtifactsUnderSrc);
    }
}
