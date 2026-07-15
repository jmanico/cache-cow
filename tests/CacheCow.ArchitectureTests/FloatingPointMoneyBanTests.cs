using System.Reflection;
using System.Text.RegularExpressions;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.ArchitectureTests;

/// <summary>
/// CC-PRC-003: binary floating point MUST NOT be used for money anywhere,
/// including tests. Two enforcement layers: the Money API surface exposes no
/// float/double member (issue 002, AC-06), and a source scan fails the build on
/// any float/double token in the shared kernel or in money-path test files
/// (issue 002, AC-04 — the build-local half of the CI gate in issue 006).
/// </summary>
public sealed partial class FloatingPointMoneyBanTests
{
    [GeneratedRegex(@"\b(float|double|Single|Double)\b")]
    private static partial Regex FloatingPointTokenPattern();

    [Fact]
    [Requirement("CC-PRC-003")]
    public void Money_api_exposes_no_binary_floating_point_member()
    {
        var offenders = new List<string>();

        foreach (var type in new[] { typeof(Money), typeof(Currency) })
        {
            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                var exposed = member switch
                {
                    MethodInfo method => method.ReturnType == typeof(float) || method.ReturnType == typeof(double)
                        || method.GetParameters().Any(p => p.ParameterType == typeof(float) || p.ParameterType == typeof(double)),
                    PropertyInfo property => property.PropertyType == typeof(float) || property.PropertyType == typeof(double),
                    FieldInfo field => field.FieldType == typeof(float) || field.FieldType == typeof(double),
                    ConstructorInfo ctor => ctor.GetParameters().Any(p => p.ParameterType == typeof(float) || p.ParameterType == typeof(double)),
                    _ => false,
                };

                if (exposed)
                {
                    offenders.Add($"{type.Name}.{member.Name}");
                }
            }
        }

        Assert.True(offenders.Count == 0, "Lossy floating-point members on money types (CC-PRC-003): " + string.Join(", ", offenders));
    }

    [Fact]
    [Requirement("CC-PRC-003")]
    public void No_floating_point_token_in_shared_kernel_or_money_tests()
    {
        var scanRoots = new[]
        {
            Path.Combine(SolutionModel.RepoRoot, "src", "CacheCow.SharedKernel"),
            Path.Combine(SolutionModel.RepoRoot, "tests", "CacheCow.SharedKernel.Tests"),
        };

        var violations = new List<string>();
        foreach (var root in scanRoots.Where(Directory.Exists))
        {
            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                foreach (var (line, index) in File.ReadAllLines(file).Select((line, index) => (line, index)))
                {
                    if (FloatingPointTokenPattern().IsMatch(line))
                    {
                        violations.Add($"{file}:{index + 1}: {line.Trim()}");
                    }
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Binary floating point in money code or tests (CC-PRC-003):\n" + string.Join('\n', violations));
    }
}
