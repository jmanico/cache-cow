using CacheCow.SharedKernel;

namespace CacheCow.Modules.CatalogInventory.Inventory;

/// <summary>
/// Inventory tracked per SKU per regional cold store (CC-CAT-002; issue 030
/// AC-01). Quantities are never negative — invalid mutations are rejected with
/// no state change (issue 030 AC-06; SECURITY.md, Input validation rule 1).
/// <see cref="RestockExpected"/> is the interim signal distinguishing
/// "restocking (preorder permitted)" from "unavailable in region" at zero
/// stock; the authoritative signal source (planned replenishment record vs.
/// manual flag) is an open question on issue 030 awaiting a human decision.
/// </summary>
public sealed class StockRecord
{
    private StockRecord(SkuId skuId, ColdStoreId coldStoreId, int quantityOnHand, bool restockExpected)
    {
        SkuId = skuId;
        ColdStoreId = coldStoreId;
        QuantityOnHand = quantityOnHand;
        RestockExpected = restockExpected;
    }

    public SkuId SkuId { get; }

    public ColdStoreId ColdStoreId { get; }

    public int QuantityOnHand { get; }

    public bool RestockExpected { get; }

    public static StockRecord Create(SkuId skuId, ColdStoreId coldStoreId, int quantityOnHand, bool restockExpected)
    {
        if (skuId == default)
        {
            throw new ArgumentException("Inventory requires an initialized SKU ID (CC-CAT-002).", nameof(skuId));
        }

        if (coldStoreId == default)
        {
            throw new ArgumentException(
                "Inventory requires an initialized cold-store ID (CC-CAT-002).", nameof(coldStoreId));
        }

        if (quantityOnHand < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantityOnHand), quantityOnHand,
                "Inventory quantity cannot be negative; the write is rejected with no state change (issue 030 AC-06).");
        }

        return new StockRecord(skuId, coldStoreId, quantityOnHand, restockExpected);
    }
}
