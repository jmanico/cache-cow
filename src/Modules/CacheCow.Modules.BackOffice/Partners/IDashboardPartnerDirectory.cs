using CacheCow.Modules.BackOffice.Dashboard;

namespace CacheCow.Modules.BackOffice.Partners;

/// <summary>A validated partner-search query (issue 085, AC-01). Closed typed shape; no user-chosen sort or filter column.</summary>
public sealed class DashboardPartnerQuery
{
    private DashboardPartnerQuery(DashboardPartnerState? state, int page, int pageSize)
    {
        State = state;
        Page = page;
        PageSize = pageSize;
    }

    /// <summary>Restrict to one onboarding state (e.g. the approver's pending queue), or null for all.</summary>
    public DashboardPartnerState? State { get; }

    /// <summary>1-based page number.</summary>
    public int Page { get; }

    /// <summary>Rows per page, already clamped to <see cref="DashboardPaging.MaxPageSize"/>.</summary>
    public int PageSize { get; }

    /// <exception cref="DashboardValidationException">Any filter or paging value is invalid.</exception>
    public static DashboardPartnerQuery Create(
        DashboardPartnerState? state = null,
        int? page = null,
        int? pageSize = null)
    {
        var (resolvedPage, resolvedPageSize) = DashboardPaging.Normalize(page, pageSize);

        if (state is { } requestedState && !Enum.IsDefined(requestedState))
        {
            throw new DashboardValidationException(
                $"Partner state {(int)requestedState} is outside the CC-WHS-002 closed set; rejected (SECURITY.md, Input validation rule 1).");
        }

        return new DashboardPartnerQuery(state, resolvedPage, resolvedPageSize);
    }
}

/// <summary>
/// The Back Office's READ port onto the Wholesale &amp; B2B context's partner
/// tenancy (issue 085; ARCHITECTURE.md, "Server bounded contexts" 6 and 8).
/// The host adapts it onto that context's module API — never its schema, which
/// the Back Office's least-privilege database role cannot reach (CC-SEC-021;
/// SECURITY.md, Secret handling rule 10).
///
/// Implementations MUST use parameterized queries, return a stable order, and
/// THROW on failure so the caller fails closed. They MUST NOT widen the row
/// with partner contact PII (see <see cref="DashboardPartnerRow"/>).
/// </summary>
public interface IDashboardPartnerDirectory
{
    /// <summary>The matching page of partners. Throws if the read fails.</summary>
    DashboardPage<DashboardPartnerRow> Search(DashboardPartnerQuery query);

    /// <summary>
    /// One partner by id, or null when it does not exist — rendered as the
    /// same 404 an unauthorized caller receives (SECURITY.md, Authentication
    /// rule 9; issue 085 AC-06).
    /// </summary>
    DashboardPartnerDetail? Find(string partnerId);
}
