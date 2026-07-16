namespace CacheCow.Modules.BackOffice.Orders;

/// <summary>
/// The dashboard's read-model view of the CC-ORD-006 order state machine:
/// <c>received → confirmed → packed → shipped → delivered</c> with
/// <c>cancelled</c> and <c>refunded</c> as terminal branches. This is display
/// and request vocabulary ONLY — transition legality is enforced exclusively
/// by the Ordering &amp; Payments context's state machine behind
/// <see cref="IDashboardOrderCommands"/>; this module never re-implements or
/// bypasses it (issue 082, Trust Boundary; ARCHITECTURE.md, Dependency rule 9).
/// </summary>
public enum DashboardOrderState
{
    Received = 0,
    Confirmed = 1,
    Packed = 2,
    Shipped = 3,
    Delivered = 4,

    /// <summary>Terminal branch (CC-ORD-006).</summary>
    Cancelled = 5,

    /// <summary>Terminal branch (CC-ORD-006).</summary>
    Refunded = 6,
}

/// <summary>
/// Closed-set name mapping for <see cref="DashboardOrderState"/>: the wire and
/// audit vocabulary uses the exact lowercase CC-ORD-006 names. Parsing is
/// fail-closed — exact ordinal match only, anything else is rejected
/// (SECURITY.md, Input validation rule 1).
/// </summary>
public static class DashboardOrderStates
{
    private static readonly DashboardOrderState[] Closed =
    [
        DashboardOrderState.Received,
        DashboardOrderState.Confirmed,
        DashboardOrderState.Packed,
        DashboardOrderState.Shipped,
        DashboardOrderState.Delivered,
        DashboardOrderState.Cancelled,
        DashboardOrderState.Refunded,
    ];

    /// <summary>Every order state, in CC-ORD-006 order.</summary>
    public static IReadOnlyList<DashboardOrderState> All => Closed;

    /// <summary>The canonical lowercase CC-ORD-006 name of a state.</summary>
    public static string NameOf(DashboardOrderState state) => state switch
    {
        DashboardOrderState.Received => "received",
        DashboardOrderState.Confirmed => "confirmed",
        DashboardOrderState.Packed => "packed",
        DashboardOrderState.Shipped => "shipped",
        DashboardOrderState.Delivered => "delivered",
        DashboardOrderState.Cancelled => "cancelled",
        DashboardOrderState.Refunded => "refunded",
        _ => throw new DashboardValidationException(
            $"Order state {(int)state} is outside the CC-ORD-006 closed set; rejected (SECURITY.md, Input validation rule 1)."),
    };

    /// <summary>Exact (ordinal, lowercase) resolution against the closed set; unknown names do not resolve.</summary>
    public static bool TryParse(string? name, out DashboardOrderState state)
    {
        foreach (var candidate in Closed)
        {
            if (string.Equals(NameOf(candidate), name, StringComparison.Ordinal))
            {
                state = candidate;
                return true;
            }
        }

        state = default;
        return false;
    }
}
