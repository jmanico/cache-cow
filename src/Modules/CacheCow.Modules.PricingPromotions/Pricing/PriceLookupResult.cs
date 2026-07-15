using CacheCow.SharedKernel;

namespace CacheCow.Modules.PricingPromotions.Pricing;

/// <summary>
/// Typed outcome of a price lookup (issue 032): a hit carries the
/// <see cref="MarketPrice"/>; a miss is a first-class state — never a default
/// value, never another market's price, never an FX-derived amount
/// (CC-PRC-001 AC-03). Reading <see cref="Price"/> on a miss fails closed.
/// </summary>
public sealed class PriceLookupResult
{
    private readonly MarketPrice? _price;

    private PriceLookupResult(SkuId sku, Market market, MarketPrice? price)
    {
        Sku = sku;
        Market = market;
        _price = price;
    }

    public SkuId Sku { get; }

    public Market Market { get; }

    /// <summary>True when the SKU has a price in the requested market.</summary>
    public bool IsPriced => _price is not null;

    /// <summary>
    /// The resolved price. Throws <see cref="PriceUnavailableException"/> on a
    /// miss: an unpriced SKU is not purchasable in that market (fail closed).
    /// </summary>
    public MarketPrice Price => _price ?? throw new PriceUnavailableException(Sku, Market);

    internal static PriceLookupResult Priced(MarketPrice price) =>
        new(price.Sku, price.Market, price);

    internal static PriceLookupResult NotPriced(SkuId sku, Market market) =>
        new(sku, market, null);
}
