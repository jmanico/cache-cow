using System.Collections.Frozen;

namespace CacheCow.Modules.BackOffice.Rbac;

/// <summary>
/// The documented, tested role–permission matrix (CC-DSH-002; SECURITY.md,
/// Authentication rule 8) as validated, immutable data.
///
/// This type ships NO content and NO defaults: which role holds which
/// permission is not authored anywhere in the specs and MUST be authored and
/// human-approved (issue 080, Open Questions — the epic's open question on
/// matrix content). Until the host supplies a matrix, the module's default
/// wiring denies every permission check (fail closed,
/// <see cref="UnconfiguredRolePermissionMatrixProvider"/>).
///
/// <see cref="Create"/> validates the supplied configuration for closure:
/// any role or permission name outside the closed sets
/// (<see cref="StaffRole"/>, <see cref="DashboardPermission"/>) is rejected
/// at load with <see cref="RbacConfigurationException"/> — never skipped,
/// defaulted, or sanitized into acceptance (SECURITY.md, Input validation
/// rule 1). Grants are exact memberships only: there is no wildcard, no
/// role hierarchy, and no implicit admin grant (least privilege).
/// </summary>
public sealed class RolePermissionMatrix
{
    private readonly FrozenDictionary<string, FrozenSet<DashboardPermission>> grantsByRole;

    private RolePermissionMatrix(FrozenDictionary<string, FrozenSet<DashboardPermission>> grantsByRole)
    {
        this.grantsByRole = grantsByRole;
    }

    /// <summary>
    /// Builds a matrix from human-authored configuration: role name to the
    /// permission names that role holds. Every name must resolve against the
    /// closed sets or the whole matrix is rejected at load (CC-DSH-002).
    /// Roles absent from the configuration hold no permissions.
    /// </summary>
    /// <exception cref="RbacConfigurationException">
    /// An unknown role name, an unknown permission name, or a null grant list.
    /// </exception>
    public static RolePermissionMatrix Create(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> permissionNamesByRoleName)
    {
        ArgumentNullException.ThrowIfNull(permissionNamesByRoleName);

        var builder = new Dictionary<string, FrozenSet<DashboardPermission>>(StringComparer.Ordinal);
        foreach (var (roleName, permissionNames) in permissionNamesByRoleName)
        {
            if (!StaffRole.TryResolve(roleName, out _))
            {
                throw new RbacConfigurationException(
                    $"Role–permission matrix names unknown role '{roleName}'; only the closed role set of CC-DSH-002 is valid. Rejected at load (SECURITY.md, Input validation rule 1).");
            }

            if (permissionNames is null)
            {
                throw new RbacConfigurationException(
                    $"Role–permission matrix entry for role '{roleName}' has no permission list; supply an explicit (possibly empty) list (CC-DSH-002).");
            }

            var permissions = new HashSet<DashboardPermission>();
            foreach (var permissionName in permissionNames)
            {
                if (!DashboardPermission.TryResolve(permissionName, out var permission))
                {
                    throw new RbacConfigurationException(
                        $"Role–permission matrix grants unknown permission '{permissionName}' to role '{roleName}'; only the closed dashboard permission set is valid. Rejected at load (SECURITY.md, Input validation rule 1).");
                }

                permissions.Add(permission);
            }

            builder[roleName] = permissions.ToFrozenSet();
        }

        return new RolePermissionMatrix(builder.ToFrozenDictionary(StringComparer.Ordinal));
    }

    /// <summary>
    /// Exact-membership lookup: true only when the matrix explicitly grants
    /// the permission to the role. No wildcard, hierarchy, or implicit grant
    /// exists — including for <see cref="StaffRole.Admin"/> (SECURITY.md,
    /// Authentication rule 8).
    /// </summary>
    public bool IsGranted(StaffRole role, DashboardPermission permission)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(permission);

        return grantsByRole.TryGetValue(role.Name, out var granted) && granted.Contains(permission);
    }

    /// <summary>
    /// The documented grants of a role, for the matrix-conformance suite and
    /// generated documentation (SECURITY.md, Authentication rule 8 —
    /// "documented, tested"). Immutable; roles absent from the configuration
    /// return the empty set.
    /// </summary>
    public IReadOnlySet<DashboardPermission> GrantsFor(StaffRole role)
    {
        ArgumentNullException.ThrowIfNull(role);

        return grantsByRole.TryGetValue(role.Name, out var granted)
            ? granted
            : FrozenSet<DashboardPermission>.Empty;
    }
}
