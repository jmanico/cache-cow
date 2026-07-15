using System.Diagnostics.CodeAnalysis;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.CatalogInventory.Inventory;

/// <summary>
/// Port over per-SKU per-cold-store inventory (CC-CAT-002). The in-memory
/// implementation backs the availability derivation until the PostgreSQL
/// persistence adapter lands (issue 015).
/// </summary>
public interface IInventoryLedger
{
    /// <summary>Upserts the stock record for the record's SKU/store pair.</summary>
    void Record(StockRecord record);

    bool TryGet(SkuId skuId, ColdStoreId coldStoreId, [MaybeNullWhen(false)] out StockRecord record);

    /// <summary>All per-store records for a SKU (queryable per store, issue 030 AC-01).</summary>
    IReadOnlyCollection<StockRecord> RecordsFor(SkuId skuId);
}
