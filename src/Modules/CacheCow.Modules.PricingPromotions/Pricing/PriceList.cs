using CacheCow.SharedKernel;

namespace CacheCow.Modules.PricingPromotions.Pricing;

/// <summary>
/// The per-SKU per-market consumer price list (CC-PRC-001): the canonical read
/// model the order service recomputes from (CC-PRC-005; ARCHITECTURE.md,
/// Dependency rule 2). Keyed strictly by (SKU, market); a SKU with no row in
/// the transacting market has no consumer price there — no FX conversion, no
/// cross-market fallback (issue 032 AC-03).
/// </summary>
public sealed class PriceList
{
    private readonly Dictionary<(SkuId Sku, Market Market), MarketPrice> _prices;

    public PriceList(IEnumerable<MarketPrice> prices)
    {
        ArgumentNullException.ThrowIfNull(prices);

        _prices = new Dictionary<(SkuId, Market), MarketPrice>();
        foreach (var price in prices)
        {
            ArgumentNullException.ThrowIfNull(price);
            if (!_prices.TryAdd((price.Sku, price.Market), price))
            {
                throw new PricingValidationException(
                    $"Duplicate price for SKU '{price.Sku.Value}' in market {price.Market.Code}; the price list is keyed (SKU, market) with exactly one row per key (CC-PRC-001).");
            }
        }
    }

    /// <summary>
    /// Typed lookup: a miss is a <see cref="PriceLookupResult"/> in the
    /// not-priced state, never a default or another market's price (CC-PRC-001).
    /// </summary>
    public PriceLookupResult Lookup(SkuId sku, Market market)
    {
        if (sku == default)
        {
            throw new PricingValidationException("A price lookup requires a SKU identity (CC-PRC-001).");
        }

        // Validates the market is a launch market before consulting the table.
        _ = LaunchMarketCurrencies.CurrencyOf(market);

        return _prices.TryGetValue((sku, market), out var price)
            ? PriceLookupResult.Priced(price)
            : PriceLookupResult.NotPriced(sku, market);
    }
}
