namespace CacheCow.Modules.WholesaleB2B.Partners;

/// <summary>
/// The single API through which partner tenancy state ever changes
/// (CC-WHS-002): an approval workflow executed from the internal dashboard with
/// no self-service activation path — every action requires a
/// <see cref="DashboardActorProof"/>, and no member of this bounded context
/// produces an <see cref="PartnerOnboardingState.Approved"/> tenant any other
/// way (issue 049, AC-02/AC-04).
///
/// Ratified transitions:
/// <c>Draft -> Submitted</c> (<see cref="Submit"/>),
/// <c>Submitted -> Approved</c> (<see cref="Approve"/>),
/// <c>Submitted -> Rejected</c> (<see cref="Reject"/>),
/// <c>Approved -> Suspended</c> (<see cref="Suspend"/>).
/// Everything else fails closed with
/// <see cref="IllegalPartnerTransitionException"/> — including reinstatement of
/// a suspended tenant and resubmission of a rejected one, for which no path is
/// specified (issue 049, Open Questions; a human decision must add them).
///
/// Every successful transition appends exactly one <see cref="PartnerAuditEvent"/>
/// through <see cref="IPartnerAuditSink"/> before the new state is produced; if
/// the append throws, the action is denied and the tenant is unchanged
/// (issue 049, AC-02; SECURITY.md, Logging rules 2 and 6).
///
/// This type enforces workflow legality, actor evidence, and auditing. HTTP
/// authentication of the dashboard staff session, the deny-by-default
/// authorization policy, and RBAC enforcement are host/back-office scope
/// (SECURITY.md, Authentication rules 1–2, 8; issues 020/080/085).
/// </summary>
public sealed class PartnerOnboardingWorkflow
{
    /// <summary>Audit action names (SECURITY.md, Logging rule 6).</summary>
    public const string SubmitAction = "partner.onboarding.submit";
    public const string ApproveAction = "partner.onboarding.approve";
    public const string RejectAction = "partner.onboarding.reject";
    public const string SuspendAction = "partner.tenancy.suspend";

    private readonly IPartnerAuditSink _auditSink;
    private readonly TimeProvider _timeProvider;

    /// <param name="auditSink">Append-only audit emission port; a failed append denies the action.</param>
    /// <param name="timeProvider">Server clock for audit timestamps; defaults to the system clock.</param>
    public PartnerOnboardingWorkflow(IPartnerAuditSink auditSink, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(auditSink);
        _auditSink = auditSink;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Submits a draft record for approval (Draft -> Submitted).</summary>
    public PartnerTenant Submit(PartnerTenant tenant, DashboardActorProof actor) =>
        Transition(tenant, actor, PartnerOnboardingState.Draft, PartnerOnboardingState.Submitted, SubmitAction);

    /// <summary>
    /// The approval action of issue 049 AC-02 (Submitted -> Approved): the only
    /// path by which any partner tenant ever becomes active.
    /// </summary>
    public PartnerTenant Approve(PartnerTenant tenant, DashboardActorProof actor) =>
        Transition(tenant, actor, PartnerOnboardingState.Submitted, PartnerOnboardingState.Approved, ApproveAction);

    /// <summary>Declines a submitted record (Submitted -> Rejected, terminal).</summary>
    public PartnerTenant Reject(PartnerTenant tenant, DashboardActorProof actor) =>
        Transition(tenant, actor, PartnerOnboardingState.Submitted, PartnerOnboardingState.Rejected, RejectAction);

    /// <summary>Deactivates an approved tenant (Approved -> Suspended, terminal until reinstatement is ratified).</summary>
    public PartnerTenant Suspend(PartnerTenant tenant, DashboardActorProof actor) =>
        Transition(tenant, actor, PartnerOnboardingState.Approved, PartnerOnboardingState.Suspended, SuspendAction);

    private PartnerTenant Transition(
        PartnerTenant tenant,
        DashboardActorProof actor,
        PartnerOnboardingState requiredFrom,
        PartnerOnboardingState to,
        string action)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(actor);

        if (tenant.State != requiredFrom)
        {
            throw new IllegalPartnerTransitionException(tenant.State, to);
        }

        // Audit first: if the append fails, the exception propagates and no
        // transitioned tenant is ever produced (issue 049, AC-02; the
        // in-database transactional pairing is the persistence issue's scope).
        _auditSink.Append(new PartnerAuditEvent(
            actor.ActorId,
            actor.Role,
            action,
            tenant.Id,
            tenant.State,
            to,
            _timeProvider.GetUtcNow()));

        return tenant.WithState(to);
    }
}
