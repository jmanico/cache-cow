namespace CacheCow.Modules.OrderingPayments.Orders;

/// <summary>Base for all order state-transition denials (CC-ORD-006). Maps to HTTP 409 problem details at an API surface (issue 021).</summary>
public abstract class OrderTransitionException : Exception
{
    protected OrderTransitionException(string message, OrderState fromState, OrderState toState)
        : base(message)
    {
        FromState = fromState;
        ToState = toState;
    }

    public OrderState FromState { get; }

    public OrderState ToState { get; }
}

/// <summary>
/// The requested transition is not legal: it skips a state, moves backward,
/// leaves a terminal state, or is a branch the ratified table does not allow
/// (CC-ORD-006). The order is never mutated.
/// </summary>
public sealed class IllegalOrderTransitionException : OrderTransitionException
{
    public IllegalOrderTransitionException(OrderState fromState, OrderState toState)
        : base(
            $"Order transition {fromState} -> {toState} is not legal (CC-ORD-006); the order remains in {fromState}.",
            fromState,
            toState)
    {
    }
}

/// <summary>
/// A branch transition (to cancelled/refunded) was attempted, but no ratified
/// <see cref="BranchTransitionTable"/> is configured. The legal source states
/// for the branches are an open decision (issue 035, Open Questions) — until a
/// human ratifies them, every branch attempt fails closed (SECURITY.md,
/// Logging rule 2).
/// </summary>
public sealed class BranchTransitionsNotRatifiedException : OrderTransitionException
{
    public BranchTransitionsNotRatifiedException(OrderState fromState, OrderState toState)
        : base(
            $"Order transition {fromState} -> {toState} denied: the branch allowed-transition table is an open decision "
            + "(issue 035, Open Questions) and no ratified BranchTransitionTable is configured; failing closed.",
            fromState,
            toState)
    {
    }
}
