using CacheCow.Modules.BackOffice.Dashboard;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.BackOffice.Inventory;

/// <summary>
/// A validated inventory query (issue 084, AC-02: filter by cold store,
/// market, and SKU). Like the order query, this is a closed typed shape with
/// no user-chosen sort or filter column, so SECURITY.md Input validation
/// rule 4's allowlist is structural.
/// </summary>
public sealed class DashboardInventoryQuery
{
    private DashboardInventoryQuery(string? coldStoreId, Market? market, SkuId? sku, int page, int pageSize)
    {
        ColdStoreId = coldStoreId;
        Market = market;
        Sku = sku;
        Page = page;
        PageSize = pageSize;
    }

    /// <summary>Restrict to one regional cold store, or null for all.</summary>
    public string? ColdStoreId { get; }

    /// <summary>Restrict to one launch market, or null for all.</summary>
    public Market? Market { get; }

    /// <summary>Restrict to one SKU, or null for all.</summary>
    public SkuId? Sku { get; }

    /// <summary>1-based page number.</summary>
    public int Page { get; }

    /// <summary>Rows per page, already clamped to <see cref="DashboardPaging.MaxPageSize"/>.</summary>
    public int PageSize { get; }

    /// <exception cref="DashboardValidationException">Any filter or paging value is invalid.</exception>
    public static DashboardInventoryQuery Create(
        string? coldStoreId = null,
        Market? market = null,
        SkuId? sku = null,
        int? page = null,
        int? pageSize = null)
    {
        var (resolvedPage, resolvedPageSize) = DashboardPaging.Normalize(page, pageSize);

        if (coldStoreId is not null)
        {
            DashboardInventoryRow.ValidateColdStoreId(coldStoreId);
        }

        return new DashboardInventoryQuery(coldStoreId, market, sku, resolvedPage, resolvedPageSize);
    }
}

/// <summary>
/// The Back Office's READ port onto the Catalog &amp; Inventory context
/// (issue 084; ARCHITECTURE.md, "Server bounded contexts" 2 and 8). The host
/// adapts it onto that context's module API — never onto its schema, which the
/// Back Office's database role cannot reach and must not be granted
/// (CC-SEC-021; SECURITY.md, Secret handling rule 10; issue 084,
/// Constraints).
///
/// READ-ONLY, AND THAT IS THE WHOLE PORT. There is deliberately no mutation
/// member — no adjustment, no stock correction, no reconciliation. CC-DSH-003
/// names the module but authors no inventory WRITE operation anywhere, and
/// REQUIREMENTS.md §17 treats unreferenced code paths as scope creep to be
/// removed or ratified first. A write surface here would also be the one place
/// the dashboard could silently contradict the storefront's availability
/// (CC-CAT-002/003). If stock corrections from the dashboard are wanted, they
/// must be ratified in REQUIREMENTS.md and then authored — a human decision
/// (issue 084, Open Questions; CLAUDE.md, working rules).
///
/// Implementations MUST use parameterized queries (SECURITY.md, Input
/// validation rule 4), return a stable order, and THROW on failure: the caller
/// fails closed and shows a generic error rather than rendering stale or
/// fabricated inventory (issue 084, Failure Behavior).
/// </summary>
public interface IDashboardInventoryReader
{
    /// <summary>The matching page of per-SKU per-cold-store rows. Throws if the read fails.</summary>
    DashboardPage<DashboardInventoryRow> Search(DashboardInventoryQuery query);
}
