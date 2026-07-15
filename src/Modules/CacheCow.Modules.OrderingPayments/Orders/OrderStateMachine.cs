namespace CacheCow.Modules.OrderingPayments.Orders;

/// <summary>
/// The single API through which order state ever changes (CC-ORD-006;
/// issue 035, AC-06).
///
/// The linear forward path is fixed data:
/// <c>received -> confirmed -> packed -> shipped -> delivered</c>. Transitions
/// are never skipped, reordered, or reversed. The terminal branches
/// (<c>cancelled</c>, <c>refunded</c>) are legal only per an explicitly
/// ratified <see cref="BranchTransitionTable"/>; the legal branch sources are
/// an open decision (issue 035, Open Questions), so without a table every
/// branch attempt fails closed with
/// <see cref="BranchTransitionsNotRatifiedException"/>.
///
/// Every successful transition appends exactly one <see cref="OrderAuditEvent"/>
/// through <see cref="IAuditSink"/> before the new state is produced; if the
/// append throws, the transition is denied and the order is unchanged
/// (issue 035, AC-03; SECURITY.md, Logging rules 2 and 6). Requested target
/// states are untrusted regardless of caller: legality is decided only against
/// the order's current persisted state.
/// </summary>
public sealed class OrderStateMachine
{
    /// <summary>Audit action name for order state transitions (SECURITY.md, Logging rule 6).</summary>
    public const string TransitionAction = "order.state.transition";

    private static readonly Dictionary<OrderState, OrderState> ForwardPath =
        new Dictionary<OrderState, OrderState>
        {
            [OrderState.Received] = OrderState.Confirmed,
            [OrderState.Confirmed] = OrderState.Packed,
            [OrderState.Packed] = OrderState.Shipped,
            [OrderState.Shipped] = OrderState.Delivered,
        };

    private readonly IAuditSink _auditSink;
    private readonly BranchTransitionTable? _branchTransitions;
    private readonly TimeProvider _timeProvider;

    /// <param name="auditSink">Append-only audit emission port; a failed append denies the transition.</param>
    /// <param name="branchTransitions">
    /// The ratified branch table, or null while the branch-legality decision is
    /// open (issue 035, Open Questions) — null means every cancelled/refunded
    /// attempt fails closed. There is deliberately no default table.
    /// </param>
    /// <param name="timeProvider">Server clock for audit timestamps; defaults to the system clock.</param>
    public OrderStateMachine(
        IAuditSink auditSink,
        BranchTransitionTable? branchTransitions = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(auditSink);
        _auditSink = auditSink;
        _branchTransitions = branchTransitions;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Applies one legal transition and returns the order in its new state.
    /// Throws <see cref="IllegalOrderTransitionException"/> for any skip,
    /// reversal, self-transition, exit from a terminal state, or non-ratified
    /// branch source; throws <see cref="BranchTransitionsNotRatifiedException"/>
    /// for any branch attempt while no table is configured. On any throw —
    /// including an audit-append failure — the order is unchanged.
    /// </summary>
    /// <param name="order">The order in its current persisted state.</param>
    /// <param name="target">Requested target state (untrusted; validated against the current state).</param>
    /// <param name="actor">
    /// The authenticated actor performing the transition (staff identity,
    /// system component, or verified-webhook principal — issue 041). Server
    /// authentication state, never a client-supplied claim.
    /// </param>
    public Order Transition(Order order, OrderState target, string actor)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);

        var from = order.State;

        if (target is OrderState.Cancelled or OrderState.Refunded)
        {
            if (IsTerminal(from))
            {
                throw new IllegalOrderTransitionException(from, target);
            }

            if (_branchTransitions is null)
            {
                throw new BranchTransitionsNotRatifiedException(from, target);
            }

            if (!_branchTransitions.Allows(from, target))
            {
                throw new IllegalOrderTransitionException(from, target);
            }
        }
        else if (!ForwardPath.TryGetValue(from, out var next) || next != target)
        {
            throw new IllegalOrderTransitionException(from, target);
        }

        // Audit first: if the append fails, the exception propagates and no
        // transitioned order is ever produced (issue 035, AC-03 — the
        // in-database transactional pairing is the persistence issue's scope).
        _auditSink.Append(new OrderAuditEvent(
            actor,
            TransitionAction,
            order.Id,
            from,
            target,
            _timeProvider.GetUtcNow()));

        return order.WithState(target);
    }

    private static bool IsTerminal(OrderState state) =>
        state is OrderState.Cancelled or OrderState.Refunded;
}
