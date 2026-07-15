namespace CacheCow.Modules.MarketGating.Enforcement;

/// <summary>
/// Every response surface the caller can declare — the exclusion channels
/// CC-MKT-003 enumerates. Gating applies identically to all of them: non-veg
/// SKUs are excluded server-side from every IN response, whatever the channel
/// (client-side hiding is non-compliant). The B2B API is a surface too
/// (CC-API-007 parity). "Recommendations" is listed although no recommendation
/// engine is specified yet (issue 025, Open Questions).
/// </summary>
public enum ResponseSurface
{
    CatalogListing = 0,
    ProductDetail = 1,
    Search = 2,
    Recommendations = 3,
    Sitemap = 4,
    StructuredData = 5,
    Feed = 6,

    /// <summary>The versioned B2B API (CC-API-007: market gating applies identically to the storefront).</summary>
    B2BApi = 7,
}
