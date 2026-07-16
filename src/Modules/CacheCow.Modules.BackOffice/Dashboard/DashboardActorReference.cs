namespace CacheCow.Modules.BackOffice.Dashboard;

/// <summary>
/// The authorized dashboard actor a command port call is performed on behalf
/// of: authenticated staff identity plus the RBAC role the dashboard
/// authorized (CC-DSH-002/004; SECURITY.md, Logging rule 6). Minted only by
/// the Back Office services AFTER <see
/// cref="Rbac.DashboardAuthorizationService"/> granted the action — never
/// from client-supplied claims (SECURITY.md, Input validation rule 3).
///
/// This is the module-local equivalent of the actor-proof types other bounded
/// contexts require on their dashboard-driven actions (e.g., the Wholesale
/// context's onboarding workflow): the host adapter translates this reference
/// into the target context's own proof type; modules never reference each
/// other (ARCHITECTURE.md, Dependency rule 9).
/// </summary>
public sealed class DashboardActorReference
{
    private DashboardActorReference(string actorId, string role)
    {
        ActorId = actorId;
        Role = role;
    }

    /// <summary>The authenticated staff identity, from server session state.</summary>
    public string ActorId { get; }

    /// <summary>The RBAC role under which the action was authorized (CC-DSH-002).</summary>
    public string Role { get; }

    public static DashboardActorReference ForAuthorizedStaff(string actorId, string role)
    {
        if (string.IsNullOrWhiteSpace(actorId))
        {
            throw new DashboardValidationException(
                "A dashboard actor reference requires the authenticated staff identity (CC-DSH-004; SECURITY.md, Logging rule 6).");
        }

        if (string.IsNullOrWhiteSpace(role))
        {
            throw new DashboardValidationException(
                "A dashboard actor reference requires the authorizing RBAC role (CC-DSH-002).");
        }

        return new DashboardActorReference(actorId, role);
    }
}
