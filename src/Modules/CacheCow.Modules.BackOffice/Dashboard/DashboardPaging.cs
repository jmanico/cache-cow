namespace CacheCow.Modules.BackOffice.Dashboard;

/// <summary>
/// The paging bounds every dashboard query is built against (SECURITY.md,
/// HTTP boundary rule 7: "clamp page sizes").
///
/// FLAGGED: the rule mandates that page sizes be clamped but ratifies no
/// number, and none of the canonical documents fixes one for staff surfaces
/// (the only ratified page-adjacent budgets are the CC-API-008 B2B rate
/// limits, which are request rates, not page sizes). The bounds below are
/// therefore a hardening bound chosen to satisfy the rule, NOT a ratified
/// requirement, and they are deliberately conservative. A human decision may
/// replace them (CLAUDE.md, working rules).
/// </summary>
public static class DashboardPaging
{
    /// <summary>Page size used when a request does not specify one.</summary>
    public const int DefaultPageSize = 25;

    /// <summary>Hard upper bound on rows per page; larger requests are clamped down to this (never rejected).</summary>
    public const int MaxPageSize = 100;

    /// <summary>
    /// Normalizes a requested page/page-size pair. A non-positive page or
    /// page size is REJECTED (it is malformed, not merely large — SECURITY.md,
    /// Input validation rule 1: reject rather than coerce); an oversized page
    /// size is CLAMPED to <see cref="MaxPageSize"/>, which is what HTTP
    /// boundary rule 7 asks for.
    /// </summary>
    /// <exception cref="DashboardValidationException">Page or page size is non-positive.</exception>
    public static (int Page, int PageSize) Normalize(int? page, int? pageSize)
    {
        var resolvedPage = page ?? 1;
        var resolvedPageSize = pageSize ?? DefaultPageSize;

        if (resolvedPage < 1 || resolvedPageSize < 1)
        {
            throw new DashboardValidationException(
                "Page and page size must be positive (SECURITY.md, Input validation rule 1).");
        }

        return (resolvedPage, Math.Min(resolvedPageSize, MaxPageSize));
    }
}
