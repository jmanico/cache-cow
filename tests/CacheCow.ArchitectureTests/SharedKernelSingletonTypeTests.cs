using System.Text.RegularExpressions;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.ArchitectureTests;

/// <summary>
/// The money, market, locale, and SKU identity types exist once, in the shared
/// kernel (ARCHITECTURE.md, Dependency rule 9): a bounded context defining a
/// parallel representation fails the build (issue 002 AC-07, issue 003 AC-06).
/// </summary>
public sealed partial class SharedKernelSingletonTypeTests
{
    private static readonly string[] KernelOwnedTypeNames = ["Money", "Currency", "Market", "Locale", "SkuId"];

    [GeneratedRegex(@"\b(?:class|struct|record|enum|interface)\s+(?<name>\w+)")]
    private static partial Regex TypeDeclarationPattern();

    [Fact]
    [Requirement("CC-PRC-003")]
    [Requirement("CC-MKT-001")]
    public void No_bounded_context_declares_a_parallel_kernel_type()
    {
        var modulesDir = Path.Combine(SolutionModel.RepoRoot, "src", "Modules");
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(modulesDir, "*.cs", SearchOption.AllDirectories))
        {
            foreach (Match match in TypeDeclarationPattern().Matches(File.ReadAllText(file)))
            {
                var name = match.Groups["name"].Value;
                if (KernelOwnedTypeNames.Contains(name, StringComparer.Ordinal))
                {
                    violations.Add($"{file}: declares '{name}'");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Kernel-owned identity/money types redeclared in a bounded context (ARCHITECTURE.md, Dependency rule 9):\n"
            + string.Join('\n', violations));
    }
}
