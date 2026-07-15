namespace CacheCow.Modules.BackOffice.Rbac;

/// <summary>
/// Source of the current role–permission matrix. The matrix is REQUIRED
/// configuration whose content needs human authoring (issue 080, Open
/// Questions); <see cref="Current"/> returns null while none is configured,
/// and <see cref="DashboardAuthorizationService"/> then denies every check
/// (fail closed, SECURITY.md, Logging rule 2).
/// </summary>
public interface IRolePermissionMatrixProvider
{
    /// <summary>The loaded, validated matrix, or null when not configured.</summary>
    RolePermissionMatrix? Current { get; }
}

/// <summary>
/// Fail-closed default until a human-authored matrix is supplied (issue 080,
/// Open Questions): no matrix exists, so every permission check denies. This
/// is deliberately not an empty or permissive matrix — absence of the
/// required configuration must be indistinguishable from "deny everything".
/// </summary>
public sealed class UnconfiguredRolePermissionMatrixProvider : IRolePermissionMatrixProvider
{
    public RolePermissionMatrix? Current => null;
}

/// <summary>
/// Serves one validated matrix instance, for host wiring once the
/// human-authored matrix content exists (issue 080, Open Questions).
/// </summary>
public sealed class ConfiguredRolePermissionMatrixProvider : IRolePermissionMatrixProvider
{
    private readonly RolePermissionMatrix matrix;

    public ConfiguredRolePermissionMatrixProvider(RolePermissionMatrix matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        this.matrix = matrix;
    }

    public RolePermissionMatrix? Current => matrix;
}
