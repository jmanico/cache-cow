using System.Collections.ObjectModel;

namespace CacheCow.Modules.BackOffice.Dashboard;

/// <summary>
/// Factory for <see cref="DashboardPage{TRow}"/>. Kept non-generic — and
/// paired with the generic type of the same name, exactly as
/// <see cref="DashboardActionResult"/> is — so the construction entry point is
/// not a static member on a generic type (CA1000) and so call sites infer
/// <c>TRow</c> from the rows they pass.
/// </summary>
public static class DashboardPage
{
    /// <summary>
    /// Creates a validated page. The rows are defensively copied, so a port
    /// cannot mutate a page after handing it over.
    /// </summary>
    /// <exception cref="DashboardValidationException">The page is internally inconsistent.</exception>
    public static DashboardPage<TRow> Create<TRow>(IReadOnlyList<TRow> items, int page, int pageSize, int totalCount)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (page < 1 || pageSize < 1 || totalCount < 0 || items.Count > pageSize || items.Count > totalCount)
        {
            throw new DashboardValidationException(
                "Inconsistent dashboard page: page and page size must be positive, and the item count may exceed neither (SECURITY.md, HTTP boundary rule 7).");
        }

        return new DashboardPage<TRow>(new ReadOnlyCollection<TRow>([.. items]), page, pageSize, totalCount);
    }

    /// <summary>An empty page, for a query that matched nothing.</summary>
    public static DashboardPage<TRow> Empty<TRow>(int page, int pageSize) =>
        Create<TRow>([], page, pageSize, totalCount: 0);
}

/// <summary>
/// One page of dashboard rows. Page sizes are clamped at query construction
/// (SECURITY.md, HTTP boundary rule 7); this type only checks internal
/// consistency so a port cannot return more rows than the page permits.
/// Construct through <see cref="DashboardPage"/>.
/// </summary>
public sealed class DashboardPage<TRow>
{
    internal DashboardPage(IReadOnlyList<TRow> items, int page, int pageSize, int totalCount)
    {
        Items = items;
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    /// <summary>The rows of this page, in the reader's stable order.</summary>
    public IReadOnlyList<TRow> Items { get; }

    /// <summary>1-based page number.</summary>
    public int Page { get; }

    /// <summary>The clamped page size the query asked for.</summary>
    public int PageSize { get; }

    /// <summary>Total matching rows across all pages.</summary>
    public int TotalCount { get; }
}
