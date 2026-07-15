using CacheCow.Modules.PricingPromotions.Rounding;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.PricingPromotions.Pricing;

/// <summary>
/// A consumer price for one SKU in one market (CC-PRC-001): keyed (SKU, market),
/// denominated in exactly the market's launch currency, stored as integer minor
/// units via the shared <see cref="Money"/> type (CC-PRC-003). DE prices
/// additionally carry the net weight needed to derive the Preisangabenverordnung
/// unit price per kilogram (CC-PRC-002); the derivation is exact integer
/// arithmetic with an explicit, caller-ratified rounding mode.
/// </summary>
public sealed record MarketPrice
{
    private const long GramsPerKilogram = 1_000;

    public MarketPrice(SkuId sku, Market market, Money unitPrice, long? netWeightGrams = null)
    {
        if (sku == default)
        {
            throw new PricingValidationException("A market price requires a SKU identity (CC-PRC-001).");
        }

        var marketCurrency = LaunchMarketCurrencies.CurrencyOf(market);
        if (!unitPrice.Currency.Equals(marketCurrency))
        {
            throw new PricingValidationException(
                $"A {market.Code} price must be denominated in {marketCurrency.Code}, not {unitPrice.Currency.Code} (CC-PRC-001; no runtime FX conversion exists).");
        }

        if (unitPrice.MinorUnits <= 0)
        {
            throw new PricingValidationException(
                "A consumer price must be a positive amount of minor units; zero and negative prices are rejected, never defaulted (CC-PRC-001).");
        }

        if (netWeightGrams is <= 0)
        {
            throw new PricingValidationException(
                "Net weight, when carried, must be a positive number of grams (CC-PRC-002).");
        }

        if (RequiresUnitPricePerKilogram(market) && netWeightGrams is null)
        {
            throw new PricingValidationException(
                $"A {market.Code} price must carry the SKU net weight so the unit price per kilogram can be derived and displayed alongside every price (Preisangabenverordnung, CC-PRC-002; fail closed).");
        }

        Sku = sku;
        Market = market;
        UnitPrice = unitPrice;
        NetWeightGrams = netWeightGrams;
    }

    public SkuId Sku { get; }

    public Market Market { get; }

    /// <summary>The consumer unit price, integer minor units in the market's currency (CC-PRC-001/003).</summary>
    public Money UnitPrice { get; }

    /// <summary>
    /// Net weight in grams (exact integer; CC-CAT-001 data carried here as the
    /// basis for unit-price-per-kg derivation). Required for DE (CC-PRC-002).
    /// </summary>
    public long? NetWeightGrams { get; }

    /// <summary>
    /// Whether the market's display convention mandates a unit price per
    /// kilogram alongside every price. CC-PRC-002 names DE (Preisangabenverordnung).
    /// </summary>
    public static bool RequiresUnitPricePerKilogram(Market market) => market == Market.DE;

    /// <summary>
    /// Derives the unit price per kilogram from the unit price and net weight
    /// (CC-PRC-002). Exact 128-bit integer arithmetic; when the division is not
    /// exact, the explicit <paramref name="rounding"/> resolves it — no rounding
    /// policy is ratified (issue 034, Open Questions), so the mode must come
    /// from configuration and an unspecified value is rejected.
    /// </summary>
    public Money UnitPricePerKilogram(RoundingMode rounding)
    {
        if (NetWeightGrams is not { } grams)
        {
            throw new PricingValidationException(
                $"SKU '{Sku.Value}' in {Market.Code} carries no net weight; the unit price per kilogram cannot be derived (CC-PRC-002; fail closed).");
        }

        var perKilogramMinorUnits = MinorUnitArithmetic.MultiplyAndDivide(
            UnitPrice.MinorUnits, GramsPerKilogram, grams, rounding);

        return Money.FromMinorUnits(perKilogramMinorUnits, UnitPrice.Currency);
    }
}
