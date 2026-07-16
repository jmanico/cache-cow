using CacheCow.Modules.BackOffice.Auditing;
using CacheCow.Modules.BackOffice.Dashboard;
using CacheCow.Modules.BackOffice.Rbac;

namespace CacheCow.Modules.BackOffice.Partners;

/// <summary>A completed partner workflow action, for the endpoint response.</summary>
public sealed record DashboardPartnerReceipt(string PartnerId, DashboardPartnerState State);

/// <summary>
/// The partner-management module of the internal dashboard (issue 085;
/// CC-DSH-003, CC-WHS-002): the UI surface of the Wholesale context's
/// onboarding approval workflow.
/// </summary>
public interface IDashboardPartnerService
{
    /// <summary>Searches partners (permission <c>partners.manage</c>).</summary>
    DashboardActionResult<DashboardPage<DashboardPartnerRow>> Search(
        StaffContext staff, DashboardPartnerQuery query, string correlationId);

    /// <summary>Reads one partner (permission <c>partners.manage</c>).</summary>
    DashboardActionResult<DashboardPartnerDetail> Find(StaffContext staff, string partnerId, string correlationId);

    /// <summary>Approves a submitted partner (permission <c>partners.approve</c>).</summary>
    DashboardActionResult<DashboardPartnerReceipt> Approve(StaffContext staff, string partnerId, string correlationId);

    /// <summary>Rejects a submitted partner (permission <c>partners.approve</c>).</summary>
    DashboardActionResult<DashboardPartnerReceipt> Reject(StaffContext staff, string partnerId, string correlationId);

    /// <summary>Suspends an approved partner (permission <c>partners.approve</c>).</summary>
    DashboardActionResult<DashboardPartnerReceipt> Suspend(StaffContext staff, string partnerId, string correlationId);
}

/// <summary>
/// Partner management (issue 085). Same fixed sequence as order management —
/// permission, then resource, then audit-before-effect, then the owning
/// context — for the same reasons (see <see cref="Orders.DashboardOrderService"/>).
///
/// PERMISSIONS. Read (list/detail) requires <c>partners.manage</c>; approve,
/// reject, and suspend require <c>partners.approve</c> — the no-self-service
/// activation gate of CC-WHS-002. FLAGGED: the closed permission set (issue
/// 080) authors exactly these two partner permissions, so suspension maps onto
/// the approval permission for want of a more specific one; whether suspension
/// deserves its own grant is not specified. Which ROLES hold either permission
/// is the matrix's to say and needs a human decision (issue 085, Open
/// Questions).
///
/// STEP-UP. Whether partner approval requires step-up re-authentication is
/// explicitly unresolved: SECURITY.md Authentication rule 2 enumerates the
/// sensitive actions as "refunds, employee-record access, role changes" and
/// says nothing about partner approval (issue 085, Open Questions: "Not
/// assumed either way"). This service therefore asserts NOTHING of its own —
/// it asks <see cref="IDashboardAuthorizationService"/>, which demands step-up
/// exactly when the permission is marked <c>RequiresRecentReauth</c>. The
/// partner permissions are not so marked today, so no step-up is demanded; if
/// a human decides otherwise, flipping that flag on the permission is the
/// entire change, and this code needs none.
///
/// TERMS ADJUSTMENT IS NOT IMPLEMENTED. Issue 085 AC-04 wants per-partner
/// payment terms adjustable from the dashboard (CC-WHS-004); this module
/// DISPLAYS the terms (<see cref="DashboardPartnerDetail.PaymentTermsNetDays"/>)
/// but authors no mutation, because the Wholesale context exposes no terms
/// -adjustment command to drive and whether the action needs step-up is part of
/// the same open question. Flagged rather than guessed (CLAUDE.md, working
/// rules).
/// </summary>
public sealed class DashboardPartnerService : IDashboardPartnerService
{
    /// <summary>Audit object type for partners.</summary>
    private const string PartnerObjectType = "partner";

    /// <summary>Audit action names. Suffixed by <see cref="DashboardAuditWriter"/> on the outcome record.</summary>
    private const string ApproveAction = "partners.approve";
    private const string RejectAction = "partners.reject";
    private const string SuspendAction = "partners.suspend";

    private readonly IDashboardAuthorizationService authorization;
    private readonly IDashboardPartnerDirectory directory;
    private readonly IDashboardPartnerWorkflow workflow;
    private readonly DashboardAuditWriter audit;

    public DashboardPartnerService(
        IDashboardAuthorizationService authorization,
        IDashboardPartnerDirectory directory,
        IDashboardPartnerWorkflow workflow,
        IAuditEventSink auditSink,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(workflow);

        this.authorization = authorization;
        this.directory = directory;
        this.workflow = workflow;
        audit = new DashboardAuditWriter(auditSink, timeProvider);
    }

    public DashboardActionResult<DashboardPage<DashboardPartnerRow>> Search(
        StaffContext staff, DashboardPartnerQuery query, string correlationId)
    {
        ArgumentNullException.ThrowIfNull(query);

        var decision = authorization.CheckPermission(staff, DashboardPermission.ManagePartners);
        if (!decision.IsGranted)
        {
            return DashboardActionResult.Denied<DashboardPage<DashboardPartnerRow>>(decision.Denial);
        }

#pragma warning disable CA1031
        try
        {
            return DashboardActionResult.Completed(directory.Search(query));
        }
        catch (Exception)
        {
            return DashboardActionResult.Unavailable<DashboardPage<DashboardPartnerRow>>();
        }
#pragma warning restore CA1031
    }

    public DashboardActionResult<DashboardPartnerDetail> Find(
        StaffContext staff, string partnerId, string correlationId)
    {
        var decision = authorization.CheckPermission(staff, DashboardPermission.ManagePartners);
        if (!decision.IsGranted)
        {
            return DashboardActionResult.Denied<DashboardPartnerDetail>(decision.Denial);
        }

        try
        {
            DashboardPartnerRow.ValidatePartnerId(partnerId);
        }
        catch (DashboardValidationException)
        {
            return DashboardActionResult.InvalidRequest<DashboardPartnerDetail>();
        }

#pragma warning disable CA1031
        try
        {
            var partner = directory.Find(partnerId);
            return partner is null
                ? DashboardActionResult.NotFound<DashboardPartnerDetail>()
                : DashboardActionResult.Completed(partner);
        }
        catch (Exception)
        {
            return DashboardActionResult.Unavailable<DashboardPartnerDetail>();
        }
#pragma warning restore CA1031
    }

    public DashboardActionResult<DashboardPartnerReceipt> Approve(
        StaffContext staff, string partnerId, string correlationId) =>
        Execute(staff, partnerId, correlationId, ApproveAction, DashboardPartnerState.Approved, workflow.Approve);

    public DashboardActionResult<DashboardPartnerReceipt> Reject(
        StaffContext staff, string partnerId, string correlationId) =>
        Execute(staff, partnerId, correlationId, RejectAction, DashboardPartnerState.Rejected, workflow.Reject);

    public DashboardActionResult<DashboardPartnerReceipt> Suspend(
        StaffContext staff, string partnerId, string correlationId) =>
        Execute(staff, partnerId, correlationId, SuspendAction, DashboardPartnerState.Suspended, workflow.Suspend);

    private DashboardActionResult<DashboardPartnerReceipt> Execute(
        StaffContext staff,
        string partnerId,
        string correlationId,
        string action,
        DashboardPartnerState requestedState,
        Func<string, DashboardActorReference, DashboardPartnerCommandResult> invokePort)
    {
        // 1. Permission — before any lookup, so a denial reveals nothing about
        //    whether the partner exists (SECURITY.md, Authentication rule 9).
        var decision = authorization.CheckPermission(staff, DashboardPermission.ApprovePartners);
        if (!decision.IsGranted)
        {
            return DashboardActionResult.Denied<DashboardPartnerReceipt>(decision.Denial);
        }

        try
        {
            DashboardPartnerRow.ValidatePartnerId(partnerId);
        }
        catch (DashboardValidationException)
        {
            return DashboardActionResult.InvalidRequest<DashboardPartnerReceipt>();
        }

        // 2. Resource — read the pre-state for the audit record's before-summary.
        DashboardPartnerDetail? partner;
#pragma warning disable CA1031
        try
        {
            partner = directory.Find(partnerId);
        }
        catch (Exception)
        {
            return DashboardActionResult.Unavailable<DashboardPartnerReceipt>();
        }
#pragma warning restore CA1031

        if (partner is null)
        {
            return DashboardActionResult.NotFound<DashboardPartnerReceipt>();
        }

        var before = $"state={partner.Summary.State}";
        var requested = $"state={requestedState}";

        // 3. AUDIT BEFORE EFFECT. A failed append denies the action with the
        //    workflow untouched: no partner is ever activated unaudited
        //    (issue 085, Failure Behavior; CC-DSH-004).
#pragma warning disable CA1031
        try
        {
            audit.AppendAttempt(
                staff, action, PartnerObjectType, partnerId, before, requested, correlationId,
                AuditRetentionClass.Standard);
        }
        catch (Exception)
        {
            return DashboardActionResult.Unavailable<DashboardPartnerReceipt>();
        }

        // 4. Effect, in the owning context.
        DashboardPartnerCommandResult result;
        try
        {
            result = invokePort(partnerId, DashboardActorReference.ForAuthorizedStaff(staff.ActorId, staff.RoleName));
        }
        catch (Exception)
        {
            audit.TryAppendOutcome(
                staff, action, PartnerObjectType, partnerId, before,
                "outcome unknown: the owning context did not answer", correlationId,
                AuditRetentionClass.Standard, DashboardAuditOutcome.Unavailable);
            return DashboardActionResult.Unavailable<DashboardPartnerReceipt>();
        }
#pragma warning restore CA1031

        switch (result.Outcome)
        {
            case DashboardPartnerCommandOutcome.Applied:
                audit.TryAppendOutcome(
                    staff, action, PartnerObjectType, partnerId, before, $"state={result.State}", correlationId,
                    AuditRetentionClass.Standard, DashboardAuditOutcome.Applied);
                return DashboardActionResult.Completed(new DashboardPartnerReceipt(partnerId, result.State));

            case DashboardPartnerCommandOutcome.PartnerNotFound:
                audit.TryAppendOutcome(
                    staff, action, PartnerObjectType, partnerId, before,
                    "no change: the owning context does not know this partner", correlationId,
                    AuditRetentionClass.Standard, DashboardAuditOutcome.Rejected);
                return DashboardActionResult.NotFound<DashboardPartnerReceipt>();

            case DashboardPartnerCommandOutcome.Rejected:
                audit.TryAppendOutcome(
                    staff, action, PartnerObjectType, partnerId, before,
                    "no change: the owning context refused the requested transition (CC-WHS-002)", correlationId,
                    AuditRetentionClass.Standard, DashboardAuditOutcome.Rejected);
                return DashboardActionResult.Conflict<DashboardPartnerReceipt>();

            default:
                audit.TryAppendOutcome(
                    staff, action, PartnerObjectType, partnerId, before,
                    "outcome unknown: unrecognized port outcome", correlationId,
                    AuditRetentionClass.Standard, DashboardAuditOutcome.Unavailable);
                return DashboardActionResult.Unavailable<DashboardPartnerReceipt>();
        }
    }
}
