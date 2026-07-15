using System.Diagnostics.CodeAnalysis;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.CatalogInventory.Inventory;

/// <summary>
/// In-memory <see cref="IInventoryLedger"/>: provisional default and test
/// double (issue 030). One record per SKU per cold store; a store serving
/// multiple markets shares the same record — no duplication per market
/// (issue 030 AC-07).
/// </summary>
public sealed class InMemoryInventoryLedger : IInventoryLedger
{
    private readonly Dictionary<(SkuId Sku, ColdStoreId Store), StockRecord> _records = [];

    public void Record(StockRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _records[(record.SkuId, record.ColdStoreId)] = record;
    }

    public bool TryGet(SkuId skuId, ColdStoreId coldStoreId, [MaybeNullWhen(false)] out StockRecord record)
    {
        if (skuId == default || coldStoreId == default)
        {
            record = null;
            return false;
        }

        return _records.TryGetValue((skuId, coldStoreId), out record);
    }

    public IReadOnlyCollection<StockRecord> RecordsFor(SkuId skuId) =>
        skuId == default
            ? []
            : _records.Values.Where(record => record.SkuId == skuId).ToArray();
}
