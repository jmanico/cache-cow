namespace CacheCow.Modules.OrderingPayments.Orders;

/// <summary>
/// The ratified allowed-transition table for the terminal branches
/// <see cref="OrderState.Cancelled"/> and <see cref="OrderState.Refunded"/>.
///
/// OPEN DECISION (issue 035, Open Questions; CLAUDE.md working rules): the
/// specs fix the linear path and name the two terminal branches, but do NOT
/// enumerate from which source states each branch is legal (may a shipped
/// order be cancelled? is refunded reachable only after delivered?). That
/// decision needs human ratification. This type therefore has NO default
/// instance and encodes no policy of its own: the host must construct it
/// explicitly from the ratified decision, and <see cref="OrderStateMachine"/>
/// fails closed on any branch transition attempted without one.
/// </summary>
public sealed class BranchTransitionTable
{
    private readonly HashSet<OrderState> _cancellableFrom;
    private readonly HashSet<OrderState> _refundableFrom;

    /// <param name="cancellableFrom">Source states from which <see cref="OrderState.Cancelled"/> is legal.</param>
    /// <param name="refundableFrom">Source states from which <see cref="OrderState.Refunded"/> is legal.</param>
    public BranchTransitionTable(
        IEnumerable<OrderState> cancellableFrom,
        IEnumerable<OrderState> refundableFrom)
    {
        _cancellableFrom = Validate(cancellableFrom, nameof(cancellableFrom));
        _refundableFrom = Validate(refundableFrom, nameof(refundableFrom));
    }

    /// <summary>
    /// Whether the ratified table allows <paramref name="from"/> to branch to
    /// <paramref name="branchTarget"/> (which must be <see cref="OrderState.Cancelled"/>
    /// or <see cref="OrderState.Refunded"/>).
    /// </summary>
    public bool Allows(OrderState from, OrderState branchTarget) =>
        branchTarget switch
        {
            OrderState.Cancelled => _cancellableFrom.Contains(from),
            OrderState.Refunded => _refundableFrom.Contains(from),
            _ => throw new ArgumentOutOfRangeException(
                nameof(branchTarget),
                branchTarget,
                "Only the cancelled/refunded branches are table-driven; the linear path is fixed data (CC-ORD-006)."),
        };

    private static HashSet<OrderState> Validate(IEnumerable<OrderState> sources, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(sources, parameterName);

        var set = new HashSet<OrderState>(sources);
        if (set.Contains(OrderState.Cancelled) || set.Contains(OrderState.Refunded))
        {
            throw new ArgumentException(
                "cancelled and refunded are terminal (CC-ORD-006); they can never be a branch source state.",
                parameterName);
        }

        return set;
    }
}
