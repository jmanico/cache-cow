namespace CacheCow.Modules.OrderingPayments.Orders;

/// <summary>
/// The order state machine's closed state set (CC-ORD-006):
/// the linear path <c>received -> confirmed -> packed -> shipped -> delivered</c>
/// plus the terminal branches <c>cancelled</c> and <c>refunded</c>.
/// No other state exists; transitions are enforced by
/// <see cref="OrderStateMachine"/> only.
/// </summary>
public enum OrderState
{
    Received = 0,
    Confirmed = 1,
    Packed = 2,
    Shipped = 3,
    Delivered = 4,

    /// <summary>Terminal branch (CC-ORD-006). Legal source states require ratification — see <see cref="BranchTransitionTable"/>.</summary>
    Cancelled = 5,

    /// <summary>Terminal branch (CC-ORD-006). Legal source states require ratification — see <see cref="BranchTransitionTable"/>.</summary>
    Refunded = 6,
}
