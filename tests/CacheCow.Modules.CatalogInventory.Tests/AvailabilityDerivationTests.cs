using CacheCow.Modules.CatalogInventory.Inventory;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.CatalogInventory.Tests;

/// <summary>
/// Issue 030: inventory is tracked per SKU per regional cold store
/// (CC-CAT-002) and derives to exactly the three user-facing states of
/// CC-CAT-003, failing closed to unavailable-in-region — never in-stock —
/// on any unresolved mapping, market, or store.
/// </summary>
public sealed class AvailabilityDerivationTests
{
    private static readonly SkuId Paneer = SkuId.Parse("VEG-PANEER-01");
    private static readonly SkuId Brisket = SkuId.Parse("BEEF-BRISKET-01");

    private static readonly ColdStoreId Chicago = ColdStoreId.Parse("COLD-US-CHI");
    private static readonly ColdStoreId Rotterdam = ColdStoreId.Parse("COLD-EU-RTM");
    private static readonly ColdStoreId Osaka = ColdStoreId.Parse("COLD-JP-OSA");

    private static (AvailabilityService Service, InMemoryInventoryLedger Ledger, InMemoryMarketColdStoreMap Map) Harness()
    {
        var ledger = new InMemoryInventoryLedger();
        var map = new InMemoryMarketColdStoreMap();
        return (new AvailabilityService(ledger, map), ledger, map);
    }

    [Fact]
    [Requirement("CC-CAT-002")]
    public void Inventory_is_tracked_independently_per_sku_per_cold_store()
    {
        var (_, ledger, _) = Harness();
        ledger.Record(StockRecord.Create(Paneer, Chicago, quantityOnHand: 12, restockExpected: false));
        ledger.Record(StockRecord.Create(Paneer, Rotterdam, quantityOnHand: 0, restockExpected: true));
        ledger.Record(StockRecord.Create(Brisket, Chicago, quantityOnHand: 7, restockExpected: false));

        Assert.True(ledger.TryGet(Paneer, Chicago, out var chicagoRecord));
        Assert.Equal(12, chicagoRecord.QuantityOnHand);

        Assert.True(ledger.TryGet(Paneer, Rotterdam, out var rotterdamRecord));
        Assert.Equal(0, rotterdamRecord.QuantityOnHand);
        Assert.True(rotterdamRecord.RestockExpected);

        Assert.Equal(2, ledger.RecordsFor(Paneer).Count);
        Assert.Single(ledger.RecordsFor(Brisket));
    }

    [Theory]
    [Requirement("CC-CAT-002")]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void A_negative_quantity_is_rejected_with_no_state_change(int negative)
    {
        var (_, ledger, _) = Harness();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StockRecord.Create(Paneer, Chicago, negative, restockExpected: false));

        Assert.Empty(ledger.RecordsFor(Paneer));
    }

    [Fact]
    [Requirement("CC-CAT-002")]
    public void Stock_records_require_initialized_identifiers()
    {
        Assert.Throws<ArgumentException>(() =>
            StockRecord.Create(default, Chicago, 1, restockExpected: false));
        Assert.Throws<ArgumentException>(() =>
            StockRecord.Create(Paneer, default, 1, restockExpected: false));
    }

    [Theory]
    [Requirement("CC-CAT-003")]
    [InlineData(120, false, SkuAvailability.InStock)]
    [InlineData(120, true, SkuAvailability.InStock)]
    [InlineData(1, false, SkuAvailability.InStock)] // boundary: last unit still ships
    [InlineData(0, true, SkuAvailability.Restocking)] // zero stock + expected restock = preorder permitted
    [InlineData(0, false, SkuAvailability.UnavailableInRegion)]
    public void The_stock_level_and_restock_flag_derive_exactly_one_closed_state(
        int quantity, bool restockExpected, SkuAvailability expected)
    {
        var (service, ledger, map) = Harness();
        map.Assign(Market.US, Chicago);
        ledger.Record(StockRecord.Create(Paneer, Chicago, quantity, restockExpected));

        Assert.Equal(expected, service.DeriveFromStock(Paneer, Market.US));
    }

    [Fact]
    [Requirement("CC-CAT-003")]
    public void The_availability_set_is_closed_to_exactly_three_states_and_defaults_fail_closed()
    {
        Assert.Equal(
            [SkuAvailability.UnavailableInRegion, SkuAvailability.Restocking, SkuAvailability.InStock],
            Enum.GetValues<SkuAvailability>());

        // The zero value is unavailable-in-region: an unset state can never
        // read as purchasable (issue 030, Failure Behavior).
        Assert.Equal(SkuAvailability.UnavailableInRegion, default(SkuAvailability));
    }

    [Fact]
    [Requirement("CC-CAT-002")]
    public void Stock_in_a_store_not_serving_the_market_never_makes_the_sku_in_stock()
    {
        var (service, ledger, map) = Harness();
        map.Assign(Market.US, Chicago);
        map.Assign(Market.DE, Rotterdam);

        // Plenty of stock — but only in the store serving DE.
        ledger.Record(StockRecord.Create(Paneer, Rotterdam, quantityOnHand: 500, restockExpected: false));

        Assert.Equal(SkuAvailability.UnavailableInRegion, service.DeriveFromStock(Paneer, Market.US));
        Assert.Equal(SkuAvailability.InStock, service.DeriveFromStock(Paneer, Market.DE));
    }

    [Fact]
    [Requirement("CC-CAT-002")]
    public void A_store_serving_multiple_markets_derives_for_each_from_one_record()
    {
        var (service, ledger, map) = Harness();
        map.Assign(Market.ES, Rotterdam);
        map.Assign(Market.DE, Rotterdam);
        ledger.Record(StockRecord.Create(Paneer, Rotterdam, quantityOnHand: 3, restockExpected: false));

        Assert.Equal(SkuAvailability.InStock, service.DeriveFromStock(Paneer, Market.ES));
        Assert.Equal(SkuAvailability.InStock, service.DeriveFromStock(Paneer, Market.DE));
        Assert.Single(ledger.RecordsFor(Paneer)); // no duplication per market (AC-07)
    }

    [Fact]
    [Requirement("CC-CAT-003")]
    public void Any_serving_store_with_stock_wins_over_a_restocking_store()
    {
        var (service, ledger, map) = Harness();
        map.Assign(Market.JP, Osaka);
        map.Assign(Market.JP, Chicago);
        ledger.Record(StockRecord.Create(Paneer, Osaka, quantityOnHand: 0, restockExpected: true));
        ledger.Record(StockRecord.Create(Paneer, Chicago, quantityOnHand: 2, restockExpected: false));

        Assert.Equal(SkuAvailability.InStock, service.DeriveFromStock(Paneer, Market.JP));
    }

    [Fact]
    [Requirement("CC-CAT-002")]
    [Requirement("CC-CAT-003")]
    public void An_unmapped_market_derives_unavailable_never_in_stock()
    {
        var (service, ledger, map) = Harness();
        map.Assign(Market.US, Chicago);
        ledger.Record(StockRecord.Create(Paneer, Chicago, quantityOnHand: 100, restockExpected: true));

        // IN has no serving cold store configured: fail closed.
        Assert.Equal(SkuAvailability.UnavailableInRegion, service.DeriveFromStock(Paneer, Market.IN));
    }

    [Fact]
    [Requirement("CC-CAT-003")]
    public void A_sku_with_no_inventory_record_in_any_serving_store_is_unavailable()
    {
        var (service, _, map) = Harness();
        map.Assign(Market.US, Chicago);

        Assert.Equal(SkuAvailability.UnavailableInRegion, service.DeriveFromStock(Paneer, Market.US));
    }

    [Fact]
    [Requirement("CC-CAT-003")]
    public void Uninitialized_identifiers_derive_unavailable_never_throw_open()
    {
        var (service, ledger, map) = Harness();
        map.Assign(Market.US, Chicago);
        ledger.Record(StockRecord.Create(Paneer, Chicago, quantityOnHand: 5, restockExpected: false));

        Assert.Equal(SkuAvailability.UnavailableInRegion, service.DeriveFromStock(default, Market.US));
        Assert.Equal(SkuAvailability.UnavailableInRegion, service.DeriveFromStock(Paneer, default));
    }

    [Fact]
    [Requirement("CC-CAT-003")]
    public void A_failing_market_store_mapping_fails_closed_to_unavailable()
    {
        var ledger = new InMemoryInventoryLedger();
        ledger.Record(StockRecord.Create(Paneer, Chicago, quantityOnHand: 50, restockExpected: false));
        var service = new AvailabilityService(ledger, new ThrowingMap());

        // An exception in the derivation path is a denial, never a bypass
        // (SECURITY.md, Logging rule 2).
        Assert.Equal(SkuAvailability.UnavailableInRegion, service.DeriveFromStock(Paneer, Market.US));
    }

    [Fact]
    [Requirement("CC-CAT-002")]
    [Requirement("CC-CAT-003")]
    public void A_sku_not_flagged_for_the_market_is_unavailable_even_with_regional_stock()
    {
        var (service, ledger, map) = Harness();
        map.Assign(Market.US, Chicago);
        map.Assign(Market.JP, Chicago);
        ledger.Record(StockRecord.Create(SkuId.Parse("US-ONLY-01"), Chicago, quantityOnHand: 10, restockExpected: false));

        var sku = new SkuBuilder()
            .WithId("US-ONLY-01")
            .WithAvailableMarkets(Market.US)
            .Build();

        Assert.Equal(SkuAvailability.InStock, service.Derive(sku, Market.US));
        Assert.Equal(SkuAvailability.UnavailableInRegion, service.Derive(sku, Market.JP));
        Assert.Equal(SkuAvailability.UnavailableInRegion, service.Derive(sku, default));
    }

    private sealed class ThrowingMap : IMarketColdStoreMap
    {
        public IReadOnlyCollection<ColdStoreId> StoresServing(Market market) =>
            throw new InvalidOperationException("Topology unavailable.");
    }
}
