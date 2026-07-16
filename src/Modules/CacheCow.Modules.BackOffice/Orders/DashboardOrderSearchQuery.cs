using CacheCow.Modules.BackOffice.Dashboard;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.BackOffice.Orders;

/// <summary>
/// A validated order-search query (issue 082, CC-DSH-003). Every filter is
/// optional; supplying none searches everything the reader exposes.
///
/// This is a CLOSED, typed shape: there is no user-chosen sort or filter
/// column anywhere on it, so the allowlist SECURITY.md Input validation rule 4
/// requires is structural rather than a runtime string check — an
/// unallowlisted column name is not representable. Result ordering is the
/// reader's own stable order, never a client's.
///
/// FLAGGED: which fields are searchable, and whether a staff member's search
/// is scoped to particular markets, is not enumerated anywhere in the
/// canonical documents beyond CC-DSH-003's bare "order management (search,
/// ...)" (issue 082, Open Questions). This shape offers the filters the issue
/// itself names — market, date range, state, order reference — and deliberately
/// does NOT invent a cross-market scoping rule; the reader port sees the query
/// as given.
/// </summary>
public sealed class DashboardOrderSearchQuery
{
    private DashboardOrderSearchQuery(
        Market? market,
        DashboardOrderState? state,
        string? orderRef,
        DateTimeOffset? placedFrom,
        DateTimeOffset? placedTo,
        int page,
        int pageSize)
    {
        Market = market;
        State = state;
        OrderRef = orderRef;
        PlacedFrom = placedFrom;
        PlacedTo = placedTo;
        Page = page;
        PageSize = pageSize;
    }

    /// <summary>Restrict to one launch market (CC-MKT-001), or null for all.</summary>
    public Market? Market { get; }

    /// <summary>Restrict to one CC-ORD-006 state, or null for all.</summary>
    public DashboardOrderState? State { get; }

    /// <summary>Exact order reference, or null. Matching semantics are the reader's.</summary>
    public string? OrderRef { get; }

    /// <summary>Inclusive lower bound on placement time, or null.</summary>
    public DateTimeOffset? PlacedFrom { get; }

    /// <summary>Inclusive upper bound on placement time, or null.</summary>
    public DateTimeOffset? PlacedTo { get; }

    /// <summary>1-based page number.</summary>
    public int Page { get; }

    /// <summary>Rows per page, already clamped to <see cref="DashboardPaging.MaxPageSize"/>.</summary>
    public int PageSize { get; }

    /// <summary>
    /// Builds a validated query. Invalid input is rejected outright, never
    /// coerced (SECURITY.md, Input validation rule 1); the page size is
    /// clamped per HTTP boundary rule 7 (see <see cref="DashboardPaging"/>).
    /// </summary>
    /// <exception cref="DashboardValidationException">Any filter or paging value is invalid.</exception>
    public static DashboardOrderSearchQuery Create(
        Market? market = null,
        DashboardOrderState? state = null,
        string? orderRef = null,
        DateTimeOffset? placedFrom = null,
        DateTimeOffset? placedTo = null,
        int? page = null,
        int? pageSize = null)
    {
        var (resolvedPage, resolvedPageSize) = DashboardPaging.Normalize(page, pageSize);

        if (orderRef is not null)
        {
            DashboardOrderRow.ValidateOrderRef(orderRef);
        }

        if (state is { } requestedState)
        {
            // Rejects values outside the CC-ORD-006 closed set (e.g. a cast
            // integer from a hand-built request).
            _ = DashboardOrderStates.NameOf(requestedState);
        }

        if (placedFrom is { } from && placedTo is { } to && from > to)
        {
            throw new DashboardValidationException(
                "An order-search date range must not end before it starts (SECURITY.md, Input validation rule 1).");
        }

        return new DashboardOrderSearchQuery(
            market, state, orderRef, placedFrom, placedTo, resolvedPage, resolvedPageSize);
    }
}
