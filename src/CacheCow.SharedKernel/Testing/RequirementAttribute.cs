namespace CacheCow.SharedKernel.Testing;

/// <summary>
/// Tags a test with the CC-* requirement IDs it verifies, enabling the
/// generated requirements-to-tests coverage report (REQUIREMENTS.md §17;
/// ARCHITECTURE.md Dependency rule 9: requirement-tagged test utilities are
/// shared kernel). Read via reflection by the traceability tooling (issue 007).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequirementAttribute : Attribute
{
    public RequirementAttribute(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
    }

    /// <summary>A CC-* requirement ID, e.g. "CC-PRC-003".</summary>
    public string Id { get; }
}
