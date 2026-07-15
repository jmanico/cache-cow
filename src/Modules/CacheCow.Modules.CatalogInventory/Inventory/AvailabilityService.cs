using CacheCow.SharedKernel;
using CacheCow.Modules.CatalogInventory.Catalog;

namespace CacheCow.Modules.CatalogInventory.Inventory;

/// <summary>
/// Derives the user-facing availability of a SKU for a requesting market from
/// the cold store(s) serving that market (CC-CAT-002, CC-CAT-003; issue 030).
/// The transacting market is server-side state resolved upstream (issue 024);
/// no client hint reaches this derivation (CC-SEC-012). Market gating
/// (CC-MKT-003) is enforced upstream of every consumer/B2B read (ARCHITECTURE.md,
/// Dependency rule 1) — a gated SKU never reaches this derivation on a
/// user-facing surface (issue 030 AC-05).
/// Fail closed everywhere: unknown market, unknown store, missing records,
/// missing availability flag, or a failing mapping port all derive
/// <see cref="SkuAvailability.UnavailableInRegion"/> — never in-stock
/// (SECURITY.md, Logging rule 2).
/// </summary>
public sealed class AvailabilityService
{
    private readonly IInventoryLedger _ledger;
    private readonly IMarketColdStoreMap _marketColdStoreMap;

    public AvailabilityService(IInventoryLedger ledger, IMarketColdStoreMap marketColdStoreMap)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(marketColdStoreMap);
        _ledger = ledger;
        _marketColdStoreMap = marketColdStoreMap;
    }

    /// <summary>
    /// Availability of a catalog SKU in the requesting market: the SKU's
    /// per-market availability flag (CC-CAT-001) composed with regional stock.
    /// </summary>
    public SkuAvailability Derive(Sku sku, Market market)
    {
        ArgumentNullException.ThrowIfNull(sku);

        if (!sku.IsAvailableIn(market))
        {
            return SkuAvailability.UnavailableInRegion;
        }

        return DeriveFromStock(sku.Id, market);
    }

    /// <summary>
    /// Availability from regional stock alone: in stock when any cold store
    /// serving the market holds quantity; restocking when a serving store has
    /// a restock expected; otherwise unavailable in region (CC-CAT-003).
    /// Stock in a store that does not serve the market never counts
    /// (issue 030 AC-02).
    /// </summary>
    public SkuAvailability DeriveFromStock(SkuId skuId, Market market)
    {
        if (skuId == default || market == default)
        {
            return SkuAvailability.UnavailableInRegion;
        }

        IReadOnlyCollection<ColdStoreId> servingStores;
        try
        {
            servingStores = _marketColdStoreMap.StoresServing(market) ?? [];
        }
#pragma warning disable CA1031 // Fail closed: any failure resolving the serving
        // relationship is a denial (unavailable), never a bypass to in-stock
        // (issue 030, Failure Behavior; SECURITY.md, Logging rule 2).
        catch (Exception)
#pragma warning restore CA1031
        {
            return SkuAvailability.UnavailableInRegion;
        }

        var restockExpected = false;
        foreach (var store in servingStores)
        {
            if (!_ledger.TryGet(skuId, store, out var record))
            {
                continue;
            }

            if (record.QuantityOnHand > 0)
            {
                return SkuAvailability.InStock;
            }

            restockExpected |= record.RestockExpected;
        }

        return restockExpected ? SkuAvailability.Restocking : SkuAvailability.UnavailableInRegion;
    }
}
