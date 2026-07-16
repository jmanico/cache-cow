using CacheCow.Modules.BackOffice.Dashboard;

namespace CacheCow.Modules.BackOffice.Partners;

/// <summary>How the Wholesale &amp; B2B context answered a partner workflow command.</summary>
public enum DashboardPartnerCommandOutcome
{
    /// <summary>The context applied the transition.</summary>
    Applied = 0,

    /// <summary>The partner does not exist (as seen by the owning context).</summary>
    PartnerNotFound = 1,

    /// <summary>
    /// The context's workflow refused: the transition is illegal from the
    /// partner's current state (e.g. approving a record that was never
    /// submitted, or suspending one that was never approved). NO state
    /// changed. This module never predicts this answer.
    /// </summary>
    Rejected = 2,
}

/// <summary>The result of a partner workflow command.</summary>
/// <param name="Outcome">What the owning context did.</param>
/// <param name="State">The partner's state after the call; meaningful only when <see cref="DashboardPartnerCommandOutcome.Applied"/>.</param>
public sealed record DashboardPartnerCommandResult(DashboardPartnerCommandOutcome Outcome, DashboardPartnerState State);

/// <summary>
/// The Back Office's COMMAND port onto the Wholesale &amp; B2B context's
/// partner onboarding workflow (issue 085; CC-WHS-002). This is the dashboard
/// UI surface of that workflow — the workflow itself is issue 049's.
///
/// HOST ADAPTER CONTRACT. The host adapts each member onto the Wholesale
/// context's <c>PartnerOnboardingWorkflow</c>, translating the
/// <see cref="DashboardActorReference"/> this module supplies into that
/// context's own <c>DashboardActorProof</c> (both carry exactly actor id +
/// role, so the mapping is total). This module MUST NOT reference the
/// Wholesale assembly — modules reference only the shared kernel
/// (ARCHITECTURE.md, Dependency rule 9) — which is precisely why the actor
/// crosses as this module's own type and the proof is minted on the far side.
///
/// NO SELF-SERVICE PATH EXISTS, and the type system is what guarantees it:
/// every member here demands an actor reference that only this module mints,
/// and only after <see cref="Rbac.IDashboardAuthorizationService"/> has granted
/// the action; the Wholesale workflow likewise has no overload without its
/// proof. So no partner is activatable from the portal, the B2B API, or the
/// storefront (CC-WHS-002; issue 085, AC-03).
///
/// WORKFLOW LEGALITY LIVES BEHIND THIS PORT, exactly as the order state
/// machine does: the ratified transitions (<c>Submitted → Approved</c>,
/// <c>Submitted → Rejected</c>, <c>Approved → Suspended</c>) are enforced
/// there, and this module neither re-implements nor pre-judges them.
///
/// Implementations MUST throw on infrastructure failure; a throw means the
/// outcome is UNKNOWN, not "no change" (SECURITY.md, Logging rule 2).
/// </summary>
public interface IDashboardPartnerWorkflow
{
    /// <summary>
    /// Approves a submitted partner — the single act that makes a partner
    /// active and able to reach wholesale price lists and case ordering
    /// (CC-WHS-001/002; issue 085, AC-02).
    /// </summary>
    DashboardPartnerCommandResult Approve(string partnerId, DashboardActorReference actor);

    /// <summary>Declines a submitted partner; the partner stays inactive (issue 085, AC-02).</summary>
    DashboardPartnerCommandResult Reject(string partnerId, DashboardActorReference actor);

    /// <summary>
    /// Deactivates an approved partner.
    ///
    /// FLAGGED: issue 085 records that the specs define onboarding APPROVAL
    /// only, and whether suspension/deactivation/offboarding is in v1 scope is
    /// not stated (issue 085, Open Questions). This member exists because the
    /// Wholesale context's workflow already authors the ratified
    /// <c>Approved → Suspended</c> transition (issue 049); it deliberately
    /// adds no new capability of its own. Reinstatement of a suspended partner
    /// is NOT offered here — no path for it is specified, and that context
    /// fails closed on it pending a human decision.
    /// </summary>
    DashboardPartnerCommandResult Suspend(string partnerId, DashboardActorReference actor);
}
