using System.Xml.Linq;

namespace CacheCow.ArchitectureTests;

/// <summary>
/// First-party model of the solution's project graph, read from the csproj
/// files themselves so the architecture tests verify the same graph the
/// compiler enforces (issue 001; no third-party dependency per SECURITY.md,
/// Dependency rule 1).
/// </summary>
public static class SolutionModel
{
    public static readonly string[] BoundedContexts =
    [
        "MarketGating",
        "CatalogInventory",
        "PricingPromotions",
        "OrderingPayments",
        "Fulfillment",
        "WholesaleB2B",
        "Invoicing",
        "BackOffice",
        "IdentityAccess",
        "ContentLocalization",
    ];

    public static string RepoRoot { get; } = FindRepoRoot();

    public static IReadOnlyList<ProjectFile> AllProjects { get; } =
        Directory.EnumerateFiles(RepoRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(ProjectFile.Load)
            .ToArray();

    public static ProjectFile Project(string name) =>
        AllProjects.Single(p => string.Equals(p.Name, name, StringComparison.Ordinal));

    public static string ModuleProjectName(string context) => $"CacheCow.Modules.{context}";

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CacheCow.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("CacheCow.sln not found above the test base directory.");
    }
}

public sealed record ProjectFile(string Name, string Path, IReadOnlyList<string> ProjectReferences)
{
    public static ProjectFile Load(string path)
    {
        var doc = XDocument.Load(path);
        var references = doc.Descendants("ProjectReference")
            .Select(r => (string?)r.Attribute("Include"))
            .Where(include => include is not null)
            .Select(include => System.IO.Path.GetFileNameWithoutExtension(include!.Replace('\\', System.IO.Path.DirectorySeparatorChar)))
            .ToArray();

        return new ProjectFile(System.IO.Path.GetFileNameWithoutExtension(path), path, references);
    }

    public bool IsTestProject => Name.EndsWith(".Tests", StringComparison.Ordinal)
        || Name.EndsWith("Tests", StringComparison.Ordinal) && Name.Contains("Architecture", StringComparison.Ordinal);
}
