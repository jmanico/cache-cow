using CacheCow.Modules.BackOffice.Dashboard;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.BackOffice.Orders;

/// <summary>How the Ordering &amp; Payments context answered a dashboard command.</summary>
public enum DashboardOrderCommandOutcome
{
    /// <summary>The context applied the change.</summary>
    Applied = 0,

    /// <summary>The order does not exist (as seen by the owning context).</summary>
    OrderNotFound = 1,

    /// <summary>
    /// The context's state machine refused: the transition is illegal under
    /// CC-ORD-006 (e.g. <c>delivered → packed</c>, or any move out of a
    /// terminal branch), or the order is not refundable. NO state changed.
    /// This module never predicts this answer — it only reports it (issue 082,
    /// AC-03).
    /// </summary>
    Rejected = 2,
}

/// <summary>The result of a transition command.</summary>
/// <param name="Outcome">What the owning context did.</param>
/// <param name="State">The order's state after the call, as reported by the owning context; meaningful only when <see cref="DashboardOrderCommandOutcome.Applied"/>.</param>
public sealed record DashboardOrderTransitionResult(DashboardOrderCommandOutcome Outcome, DashboardOrderState State);

/// <summary>The result of a refund command.</summary>
/// <param name="Outcome">What the owning context did.</param>
/// <param name="State">The order's state after the call; <see cref="DashboardOrderState.Refunded"/> when applied (CC-ORD-006).</param>
/// <param name="RefundedAmount">
/// The amount the owning context actually refunded, in integer minor units
/// (CC-PRC-003). It is an OUTPUT, never an input: the amount is canonical data
/// recomputed by the Ordering context from the order (CC-PRC-005), so no
/// client and no dashboard operator can influence it.
/// </param>
public sealed record DashboardOrderRefundResult(
    DashboardOrderCommandOutcome Outcome,
    DashboardOrderState State,
    Money RefundedAmount);

/// <summary>
/// The Back Office's COMMAND port onto the Ordering &amp; Payments context
/// (issue 082). The host adapts it onto that context's order state machine
/// (issue 035) and refund path (executed against Stripe/Razorpay by issues
/// 039/040 — never called from dashboard code).
///
/// THE STATE MACHINE LIVES BEHIND THIS PORT. The CC-ORD-006 machine
/// (<c>received → confirmed → packed → shipped → delivered</c>, with
/// <c>cancelled</c> and <c>refunded</c> as terminal branches) is enforced
/// exclusively by the Ordering &amp; Payments context. This module:
/// <list type="bullet">
/// <item>never re-implements it — <see cref="DashboardOrderState"/> is display
/// and request vocabulary, and no legality table exists in this module;</item>
/// <item>never bypasses it — no dashboard code mutates an order (issue 082,
/// Trust Boundary; ARCHITECTURE.md, Dependency rule 6);</item>
/// <item>never pre-judges it — an illegal transition is refused by the machine
/// and surfaced as <see cref="DashboardOrderCommandOutcome.Rejected"/>. A
/// duplicated legality check here would be a second source of truth that could
/// drift from the real one, and drift toward permissive is a defect
/// (ARCHITECTURE.md, Dependency rule 1's reasoning, applied to order
/// state).</item>
/// </list>
///
/// Implementations MUST apply the change atomically and MUST throw on
/// infrastructure failure; the caller then fails closed (SECURITY.md, Logging
/// rule 2). A throw means the outcome is UNKNOWN, not "nothing happened".
/// </summary>
public interface IDashboardOrderCommands
{
    /// <summary>
    /// Requests a CC-ORD-006 transition on behalf of an already-authorized,
    /// already-audited dashboard actor.
    /// </summary>
    /// <param name="orderRef">The order to transition.</param>
    /// <param name="targetState">The requested state; legality is the machine's to decide.</param>
    /// <param name="actor">Proof the Back Office authorized this actor (see <see cref="DashboardActorReference"/>).</param>
    DashboardOrderTransitionResult Transition(
        string orderRef,
        DashboardOrderState targetState,
        DashboardActorReference actor);

    /// <summary>
    /// Requests a refund on behalf of an already-authorized, step-up-verified,
    /// already-audited dashboard actor. The order reaches the terminal
    /// <c>refunded</c> branch via the state machine (CC-ORD-006, issue 082
    /// AC-05).
    ///
    /// NO AMOUNT PARAMETER, deliberately. The dashboard MUST NOT accept or
    /// forward client-supplied monetary values (issue 082, Anti-Patterns;
    /// CC-PRC-005; ARCHITECTURE.md, Dependency rule 2), and partial or
    /// per-line-item refunds are not specified anywhere in the canonical
    /// documents — CC-ORD-006 defines only a terminal <c>refunded</c> ORDER
    /// state (issue 082, Open Questions). Adding an amount here would silently
    /// invent partial-refund semantics, so the port refunds the order and
    /// REPORTS the canonical amount back. If partial refunds are ratified, a
    /// human decision extends this port.
    /// </summary>
    DashboardOrderRefundResult Refund(string orderRef, DashboardActorReference actor);
}
