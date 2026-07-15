namespace CacheCow.Modules.WholesaleB2B.Partners;

/// <summary>
/// Evidence that an onboarding action is being performed by an authenticated,
/// RBAC-authorized member of internal dashboard staff (CC-WHS-002; issue 049,
/// AC-02/AC-04). Every <see cref="PartnerOnboardingWorkflow"/> action requires
/// one — there is no overload without it, which is what makes a self-service
/// activation path unrepresentable inside this bounded context.
///
/// Construction authority: only the Back Office dashboard boundary — separate
/// origin, VPN-restricted, SSO with mandatory passkeys (SECURITY.md, HTTP
/// boundary rule 8; Authentication rule 2) — may mint one, from its
/// server-side authenticated staff session, never from client-supplied claims.
/// Which dashboard role(s) hold the partner-approval permission is an open
/// decision (issue 049, Open Questions; role–permission matrix, issue 080), as
/// is whether approval requires step-up re-authentication; this type carries
/// the actor and role for the audit record and does not resolve either.
/// </summary>
public sealed class DashboardActorProof
{
    private DashboardActorProof(string actorId, string role)
    {
        ActorId = actorId;
        Role = role;
    }

    /// <summary>The authenticated staff identity, from server session state (audited per SECURITY.md, Logging rule 6).</summary>
    public string ActorId { get; }

    /// <summary>The RBAC role the dashboard authorized for this action (CC-DSH-002).</summary>
    public string Role { get; }

    public static DashboardActorProof ForAuthenticatedStaff(string actorId, string role)
    {
        if (string.IsNullOrWhiteSpace(actorId))
        {
            throw new WholesaleValidationException(
                "A dashboard actor proof requires the authenticated staff identity (CC-WHS-002; SECURITY.md, Logging rule 6).");
        }

        if (string.IsNullOrWhiteSpace(role))
        {
            throw new WholesaleValidationException(
                "A dashboard actor proof requires the authorizing RBAC role (CC-DSH-002).");
        }

        return new DashboardActorProof(actorId, role);
    }
}
