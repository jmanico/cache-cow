namespace CacheCow.Modules.BackOffice.Rbac;

/// <summary>
/// The single server-side enforcement point for dashboard RBAC (CC-DSH-002;
/// SECURITY.md, Authentication rule 8). Every dashboard endpoint's named
/// authorization policy resolves through this check; the Angular client never
/// gates (issue 080, Trust Boundary). Deny-by-default fallback for
/// unannotated endpoints is issue 020's.
/// </summary>
public interface IDashboardAuthorizationService
{
    /// <summary>
    /// Checks whether the authenticated staff context holds the permission
    /// under the configured role–permission matrix, including step-up
    /// re-authentication recency for sensitive permissions (SECURITY.md,
    /// Authentication rule 2). Fails closed on every abnormal condition —
    /// missing matrix, unknown role, missing step-up policy, or any internal
    /// exception — returning a denial, never throwing and never bypassing
    /// (SECURITY.md, Logging rule 2).
    /// </summary>
    AccessDecision CheckPermission(StaffContext staff, DashboardPermission permission);
}
