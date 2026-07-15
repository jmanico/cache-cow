namespace CacheCow.Modules.Fulfillment.Routing;

/// <summary>
/// Outcome of routing an order to its regional cold store (CC-FUL-001).
/// Either an assignment exists or a typed failure reason does — there is no
/// way to express a fallback or partial assignment (issue 044, AC-06).
/// </summary>
public sealed record RoutingResult
{
    private RoutingResult(ColdStoreAssignment? assignment, RoutingFailureReason failure)
    {
        Assignment = assignment;
        Failure = failure;
    }

    public ColdStoreAssignment? Assignment { get; }

    /// <summary><see cref="RoutingFailureReason.None"/> when routed.</summary>
    public RoutingFailureReason Failure { get; }

    public bool IsRouted => Assignment is not null;

    public static RoutingResult Routed(ColdStoreAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        return new RoutingResult(assignment, RoutingFailureReason.None);
    }

    public static RoutingResult Failed(RoutingFailureReason failure) =>
        failure == RoutingFailureReason.None
            ? throw new ArgumentOutOfRangeException(nameof(failure), "A failed result requires a reason.")
            : new RoutingResult(null, failure);
}
