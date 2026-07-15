namespace CacheCow.Modules.BackOffice.Rbac;

/// <summary>
/// Matrix-driven dashboard permission enforcement (CC-DSH-002; SECURITY.md,
/// Authentication rule 8). Grants exist only as explicit matrix memberships:
/// no wildcard, no role hierarchy, no implicit admin. Sensitive permissions
/// additionally require re-authentication within the configured step-up max
/// age (SECURITY.md, Authentication rule 2). Every abnormal condition is a
/// denial (SECURITY.md, Logging rule 2).
/// </summary>
public sealed class DashboardAuthorizationService : IDashboardAuthorizationService
{
    private readonly IRolePermissionMatrixProvider matrixProvider;
    private readonly IStepUpPolicyProvider stepUpPolicyProvider;
    private readonly TimeProvider timeProvider;

    public DashboardAuthorizationService(
        IRolePermissionMatrixProvider matrixProvider,
        IStepUpPolicyProvider stepUpPolicyProvider,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(matrixProvider);
        ArgumentNullException.ThrowIfNull(stepUpPolicyProvider);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.matrixProvider = matrixProvider;
        this.stepUpPolicyProvider = stepUpPolicyProvider;
        this.timeProvider = timeProvider;
    }

    public AccessDecision CheckPermission(StaffContext staff, DashboardPermission permission)
    {
        // Any exception anywhere in this authorization path — a throwing
        // provider, a null argument, anything unforeseen — is a denial,
        // never a bypass and never an unhandled escape into the caller
        // (SECURITY.md, Logging rule 2). Catching the general exception type
        // is deliberate: fail closed.
#pragma warning disable CA1031
        try
        {
            ArgumentNullException.ThrowIfNull(staff);
            ArgumentNullException.ThrowIfNull(permission);

            var matrix = matrixProvider.Current;
            if (matrix is null)
            {
                // The matrix content is required human-authored configuration
                // that does not exist yet (issue 080, Open Questions).
                return AccessDecision.Deny(AccessDenialReason.MatrixNotConfigured);
            }

            if (!StaffRole.TryResolve(staff.RoleName, out var role))
            {
                return AccessDecision.Deny(AccessDenialReason.UnknownRole);
            }

            if (!matrix.IsGranted(role, permission))
            {
                return AccessDecision.Deny(AccessDenialReason.PermissionNotGranted);
            }

            if (permission.RequiresRecentReauth)
            {
                return CheckStepUp(staff);
            }

            return AccessDecision.Granted;
        }
        catch (Exception)
        {
            return AccessDecision.Deny(AccessDenialReason.AuthorizationFault);
        }
#pragma warning restore CA1031
    }

    private AccessDecision CheckStepUp(StaffContext staff)
    {
        var policy = stepUpPolicyProvider.Current;
        if (policy is null)
        {
            // The step-up max age is required configuration with no ratified
            // number (StepUpPolicy docs); sensitive actions deny until a
            // human supplies it.
            return AccessDecision.Deny(AccessDenialReason.StepUpPolicyNotConfigured);
        }

        if (staff.LastReauthenticatedAt is not { } reauthenticatedAt)
        {
            return AccessDecision.Deny(AccessDenialReason.ReauthenticationMissing);
        }

        var now = timeProvider.GetUtcNow();
        if (reauthenticatedAt > now || now - reauthenticatedAt > policy.MaxReauthAge)
        {
            // A future-dated re-authentication is a clock anomaly or forgery
            // attempt: fail closed rather than treating it as fresh.
            return AccessDecision.Deny(AccessDenialReason.ReauthenticationStale);
        }

        return AccessDecision.Granted;
    }
}
