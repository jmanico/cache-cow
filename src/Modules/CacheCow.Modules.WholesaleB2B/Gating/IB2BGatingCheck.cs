using CacheCow.SharedKernel;

namespace CacheCow.Modules.WholesaleB2B.Gating;

/// <summary>A gating verdict for one SKU in one market. Only <see cref="Permitted"/> allows anything.</summary>
public enum B2BGatingDecision
{
    /// <summary>Denied is the zero value so an uninitialized decision denies (fail closed).</summary>
    Denied = 0,

    Permitted = 1,
}

/// <summary>
/// Port to the Market &amp; Gating Policy bounded context — the single
/// server-side enforcement point (ARCHITECTURE.md, Dependency rule 1). The
/// host adapts this to the MarketGating service; this module implements NO
/// market conditional of its own (CC-MKT-006) and consults the port for every
/// catalog read and every order line, so a partner transacting in the IN
/// market cannot see or order a non-veg SKU through any B2B endpoint
/// (CC-API-007, CC-MKT-003) — parity with the storefront.
///
/// Contract: <paramref name="market"/> is the server-side transacting market
/// from the partner's tenancy, never a client hint (SECURITY.md,
/// Authentication rule 10). Implementations return
/// <see cref="B2BGatingDecision.Denied"/> for gated, unknown, or
/// unclassifiable SKUs. Callers treat any thrown exception as a denial
/// (SECURITY.md, Logging rule 2); denials surface as 404 on read paths
/// (CC-MKT-004) and as rejection without state change on order paths.
/// </summary>
public interface IB2BGatingCheck
{
    B2BGatingDecision EvaluateSku(Market market, SkuId sku);
}
