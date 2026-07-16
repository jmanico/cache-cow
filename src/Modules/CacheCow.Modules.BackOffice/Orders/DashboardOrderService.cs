using CacheCow.Modules.BackOffice.Auditing;
using CacheCow.Modules.BackOffice.Dashboard;
using CacheCow.Modules.BackOffice.Rbac;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.BackOffice.Orders;

/// <summary>A completed transition, for the endpoint response.</summary>
public sealed record DashboardOrderTransitionReceipt(string OrderRef, DashboardOrderState State);

/// <summary>A completed refund, for the endpoint response. The amount is the owning context's canonical figure (CC-PRC-005).</summary>
public sealed record DashboardOrderRefundReceipt(string OrderRef, DashboardOrderState State, Money RefundedAmount);

/// <summary>
/// The order-management module of the internal dashboard (issue 082;
/// CC-DSH-003). Search, CC-ORD-006 state transitions, and refunds — each one
/// permission-checked server-side, each state change audited before it
/// happens, and every mutation delegated to the Ordering &amp; Payments context.
/// </summary>
public interface IDashboardOrderService
{
    /// <summary>Searches orders (permission <c>orders.search</c>).</summary>
    DashboardActionResult<DashboardPage<DashboardOrderRow>> Search(
        StaffContext staff, DashboardOrderSearchQuery query, string correlationId);

    /// <summary>Requests a CC-ORD-006 transition (permission <c>orders.transition</c>).</summary>
    DashboardActionResult<DashboardOrderTransitionReceipt> Transition(
        StaffContext staff, string orderRef, DashboardOrderState targetState, string correlationId);

    /// <summary>Requests a refund (permission <c>orders.refund</c> — sensitive: step-up re-auth required).</summary>
    DashboardActionResult<DashboardOrderRefundReceipt> Refund(
        StaffContext staff, string orderRef, string correlationId);
}

/// <summary>
/// Order management (issue 082). Every entry point runs the same fixed
/// sequence, and the ORDER of the steps is the security property:
///
/// <list type="number">
/// <item><b>Permission first</b>, through the single
/// <see cref="IDashboardAuthorizationService"/> enforcement point (CC-DSH-002;
/// SECURITY.md, Authentication rule 8). It fails closed with no matrix, an
/// unknown role, an ungranted permission, or a missing/stale step-up
/// (refunds). Because it runs BEFORE any lookup, a denial cannot reveal
/// whether the order exists.</item>
/// <item><b>Resource next.</b> An unknown order is a 404 — the same 404 the
/// denial produced (SECURITY.md, Authentication rule 9; issue 082
/// AC-06).</item>
/// <item><b>Audit before effect.</b> The attempt event is appended, and if
/// that append throws the action is denied and the port is NEVER invoked
/// (issue 081 Failure Behavior; CC-DSH-004). No unaudited state change is
/// reachable.</item>
/// <item><b>Effect last</b>, in the owning context — which alone rules on
/// CC-ORD-006 legality — followed by the compensating outcome record
/// (<see cref="DashboardAuditWriter"/>).</item>
/// </list>
///
/// Reads (<see cref="Search"/>) write no audit event: CC-DSH-004 and
/// SECURITY.md Logging rule 6 mandate auditing for privileged ACTIONS and
/// order state transitions, and issue 082's compliance test asks for evidence
/// on "every state transition and refund". FLAGGED: whether staff READS of
/// customer order data should also be audited is not stated for orders (it is
/// only mandated for employee records, CC-DSH-005); not assumed here.
/// </summary>
public sealed class DashboardOrderService : IDashboardOrderService
{
    /// <summary>Audit object type for orders.</summary>
    private const string OrderObjectType = "order";

    private readonly IDashboardAuthorizationService authorization;
    private readonly IDashboardOrderReader reader;
    private readonly IDashboardOrderCommands commands;
    private readonly DashboardAuditWriter audit;

    public DashboardOrderService(
        IDashboardAuthorizationService authorization,
        IDashboardOrderReader reader,
        IDashboardOrderCommands commands,
        IAuditEventSink auditSink,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(commands);

        this.authorization = authorization;
        this.reader = reader;
        this.commands = commands;
        audit = new DashboardAuditWriter(auditSink, timeProvider);
    }

    public DashboardActionResult<DashboardPage<DashboardOrderRow>> Search(
        StaffContext staff, DashboardOrderSearchQuery query, string correlationId)
    {
        ArgumentNullException.ThrowIfNull(query);

        var decision = authorization.CheckPermission(staff, DashboardPermission.SearchOrders);
        if (!decision.IsGranted)
        {
            return DashboardActionResult.Denied<DashboardPage<DashboardOrderRow>>(decision.Denial);
        }

        // A reader fault must never render as "no orders": an empty grid is
        // indistinguishable from a true empty result and would be a silently
        // wrong operational picture. Fail closed (SECURITY.md, Logging rule 2).
#pragma warning disable CA1031
        try
        {
            return DashboardActionResult.Completed(reader.Search(query));
        }
        catch (Exception)
        {
            return DashboardActionResult.Unavailable<DashboardPage<DashboardOrderRow>>();
        }
#pragma warning restore CA1031
    }

    public DashboardActionResult<DashboardOrderTransitionReceipt> Transition(
        StaffContext staff, string orderRef, DashboardOrderState targetState, string correlationId)
    {
        var prepared = Prepare<DashboardOrderTransitionReceipt>(
            staff, orderRef, DashboardPermission.TransitionOrders, out var order);
        if (prepared is not null)
        {
            return prepared;
        }

        string targetName;
        try
        {
            // Rejects a target outside the CC-ORD-006 closed set before the
            // audit trail or the port ever sees it. This is closed-set input
            // validation, NOT a legality check: whether received → delivered
            // is *allowed* remains entirely the state machine's ruling.
            targetName = DashboardOrderStates.NameOf(targetState);
        }
        catch (DashboardValidationException)
        {
            return DashboardActionResult.InvalidRequest<DashboardOrderTransitionReceipt>();
        }

        var before = StateSummary(order!);
        var requested = $"state={targetName}";

        return Execute(
            staff,
            DashboardPermission.TransitionOrders.Name,
            orderRef,
            before,
            requested,
            correlationId,
            AuditRetentionClass.Standard,
            () => commands.Transition(orderRef, targetState, ActorFor(staff)),
            result => (result.Outcome, $"state={DashboardOrderStates.NameOf(result.State)}",
                new DashboardOrderTransitionReceipt(orderRef, result.State)));
    }

    public DashboardActionResult<DashboardOrderRefundReceipt> Refund(
        StaffContext staff, string orderRef, string correlationId)
    {
        // The step-up recency demand rides inside this check: orders.refund is
        // marked RequiresRecentReauth, so a stale or absent re-authentication
        // denies here — before the audit trail and before the port. A refund
        // on session trust alone is unreachable (issue 082, AC-04;
        // SECURITY.md, Authentication rule 2).
        var prepared = Prepare<DashboardOrderRefundReceipt>(
            staff, orderRef, DashboardPermission.IssueRefunds, out var order);
        if (prepared is not null)
        {
            return prepared;
        }

        // The order total is recorded as pre-state CONTEXT, not as a refund
        // amount: the refunded figure is the owning context's to compute
        // (CC-PRC-005) and is written to the audit trail on the outcome event.
        var before = $"{StateSummary(order!)};total={MoneySummary(order!.Total)}";

        return Execute(
            staff,
            DashboardPermission.IssueRefunds.Name,
            orderRef,
            before,
            $"state={DashboardOrderStates.NameOf(DashboardOrderState.Refunded)}",
            correlationId,
            // A refund is money movement: financial retention (7 years,
            // ratified 2026-07-15 — CC-DSH-004).
            AuditRetentionClass.Financial,
            () => commands.Refund(orderRef, ActorFor(staff)),
            result => (result.Outcome,
                $"state={DashboardOrderStates.NameOf(result.State)};refunded={MoneySummary(result.RefundedAmount)}",
                new DashboardOrderRefundReceipt(orderRef, result.State, result.RefundedAmount)));
    }

    /// <summary>
    /// Steps 1–2 for a command: permission, then resource. Returns null when
    /// the caller may proceed, otherwise the terminating result.
    /// </summary>
    private DashboardActionResult<TReceipt>? Prepare<TReceipt>(
        StaffContext staff,
        string orderRef,
        DashboardPermission permission,
        out DashboardOrderRow? order)
    {
        order = null;

        var decision = authorization.CheckPermission(staff, permission);
        if (!decision.IsGranted)
        {
            return DashboardActionResult.Denied<TReceipt>(decision.Denial);
        }

        try
        {
            DashboardOrderRow.ValidateOrderRef(orderRef);
        }
        catch (DashboardValidationException)
        {
            return DashboardActionResult.InvalidRequest<TReceipt>();
        }

#pragma warning disable CA1031
        try
        {
            order = reader.Find(orderRef);
        }
        catch (Exception)
        {
            return DashboardActionResult.Unavailable<TReceipt>();
        }
#pragma warning restore CA1031

        // Identical to the denial response above (SECURITY.md, Authentication
        // rule 9): an IDOR probe cannot distinguish "not yours" from "no such
        // order".
        return order is null ? DashboardActionResult.NotFound<TReceipt>() : null;
    }

    /// <summary>Steps 3–4: audit before effect, effect, outcome record.</summary>
    private DashboardActionResult<TReceipt> Execute<TResult, TReceipt>(
        StaffContext staff,
        string action,
        string orderRef,
        string beforeSummary,
        string requestedSummary,
        string correlationId,
        AuditRetentionClass retentionClass,
        Func<TResult> invokePort,
        Func<TResult, (DashboardOrderCommandOutcome Outcome, string AfterSummary, TReceipt Receipt)> interpret)
    {
        // AUDIT BEFORE EFFECT. A throw here denies the action with the port
        // untouched — no privileged action completes unaudited (CC-DSH-004).
#pragma warning disable CA1031
        try
        {
            audit.AppendAttempt(
                staff, action, OrderObjectType, orderRef, beforeSummary, requestedSummary, correlationId, retentionClass);
        }
        catch (Exception)
        {
            return DashboardActionResult.Unavailable<TReceipt>();
        }

        TResult portResult;
        try
        {
            portResult = invokePort();
        }
        catch (Exception)
        {
            // The port faulted: the outcome is UNKNOWN. Record exactly that —
            // claiming "no change" would be a fabrication, since the context
            // may well have applied it before failing to answer.
            audit.TryAppendOutcome(
                staff, action, OrderObjectType, orderRef, beforeSummary,
                "outcome unknown: the owning context did not answer", correlationId, retentionClass,
                DashboardAuditOutcome.Unavailable);
            return DashboardActionResult.Unavailable<TReceipt>();
        }
#pragma warning restore CA1031

        var (outcome, afterSummary, receipt) = interpret(portResult);

        switch (outcome)
        {
            case DashboardOrderCommandOutcome.Applied:
                audit.TryAppendOutcome(
                    staff, action, OrderObjectType, orderRef, beforeSummary, afterSummary, correlationId,
                    retentionClass, DashboardAuditOutcome.Applied);
                return DashboardActionResult.Completed(receipt);

            case DashboardOrderCommandOutcome.OrderNotFound:
                // The owning context disagrees with the reader (a race, or a
                // replica lag): present it exactly as any other unknown order.
                audit.TryAppendOutcome(
                    staff, action, OrderObjectType, orderRef, beforeSummary,
                    "no change: the owning context does not know this order", correlationId, retentionClass,
                    DashboardAuditOutcome.Rejected);
                return DashboardActionResult.NotFound<TReceipt>();

            case DashboardOrderCommandOutcome.Rejected:
                audit.TryAppendOutcome(
                    staff, action, OrderObjectType, orderRef, beforeSummary,
                    "no change: the owning context refused the requested transition (CC-ORD-006)",
                    correlationId, retentionClass, DashboardAuditOutcome.Rejected);
                return DashboardActionResult.Conflict<TReceipt>();

            default:
                // An outcome this module does not understand must not become a
                // success (SECURITY.md, Logging rule 2).
                audit.TryAppendOutcome(
                    staff, action, OrderObjectType, orderRef, beforeSummary,
                    "outcome unknown: unrecognized port outcome", correlationId, retentionClass,
                    DashboardAuditOutcome.Unavailable);
                return DashboardActionResult.Unavailable<TReceipt>();
        }
    }

    private static DashboardActorReference ActorFor(StaffContext staff) =>
        DashboardActorReference.ForAuthorizedStaff(staff.ActorId, staff.RoleName);

    private static string StateSummary(DashboardOrderRow order) =>
        $"state={DashboardOrderStates.NameOf(order.State)}";

    /// <summary>
    /// Integer minor units and the currency code — the only representation
    /// money takes in this module, in logs included (CC-PRC-003: no binary
    /// floating point anywhere, including tests).
    /// </summary>
    private static string MoneySummary(Money money) =>
        $"{money.MinorUnits} {money.Currency.Code} (minor units)";
}
