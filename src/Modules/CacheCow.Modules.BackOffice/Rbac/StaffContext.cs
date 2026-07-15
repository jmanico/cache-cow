namespace CacheCow.Modules.BackOffice.Rbac;

/// <summary>
/// The server-side authenticated staff context a permission check runs
/// against (CC-DSH-001/002). Minted only from the dashboard's authenticated
/// SSO session — Entra ID staff identity with mandatory passkeys (SECURITY.md,
/// Authentication rule 2; ARCHITECTURE.md, "Authentication model") — never
/// from client-supplied claims (SECURITY.md, Input validation rule 3).
///
/// The role travels as the raw claim string: resolution against the closed
/// role set happens inside <see cref="DashboardAuthorizationService"/>, so an
/// unknown role is a denial, not a construction-time crash in a request path.
/// </summary>
public sealed class StaffContext
{
    private StaffContext(string actorId, string roleName, DateTimeOffset? lastReauthenticatedAt)
    {
        ActorId = actorId;
        RoleName = roleName;
        LastReauthenticatedAt = lastReauthenticatedAt;
    }

    /// <summary>The authenticated staff identity, from server session state (audited per SECURITY.md, Logging rule 6).</summary>
    public string ActorId { get; }

    /// <summary>The role claim from the SSO session. May be unknown; unknown roles are denied (CC-DSH-002).</summary>
    public string RoleName { get; }

    /// <summary>
    /// When this staff member last completed a step-up re-authentication
    /// ceremony in the current session (SECURITY.md, Authentication rule 2),
    /// server-recorded; null when none has occurred. Sensitive permissions
    /// deny when this is null or older than the configured step-up max age.
    /// </summary>
    public DateTimeOffset? LastReauthenticatedAt { get; }

    public static StaffContext ForAuthenticatedStaff(
        string actorId,
        string roleName,
        DateTimeOffset? lastReauthenticatedAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentNullException.ThrowIfNull(roleName);

        return new StaffContext(actorId, roleName, lastReauthenticatedAt);
    }
}
