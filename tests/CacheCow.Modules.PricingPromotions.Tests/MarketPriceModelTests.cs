using CacheCow.Modules.PricingPromotions.Pricing;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.PricingPromotions.Tests;

/// <summary>
/// Issue 032: per-SKU per-market price model — prices keyed (SKU, market) in
/// exactly the market's currency, integer minor units, typed miss on lookup,
/// no runtime FX conversion, overflow fails closed.
/// </summary>
public sealed class MarketPriceModelTests
{
    private static readonly SkuId Brisket = SkuId.Parse("SKU-BRISKET-01");
    private static readonly SkuId Paneer = SkuId.Parse("SKU-PANEER-01");

    private static MarketPrice Price(string market, long minorUnits, long? netWeightGrams = null)
    {
        var parsed = Market.Parse(market);
        return new MarketPrice(
            Brisket,
            parsed,
            Money.FromMinorUnits(minorUnits, LaunchMarketCurrencies.CurrencyOf(parsed)),
            netWeightGrams ?? (parsed == Market.DE ? 1_000 : null));
    }

    [Theory]
    [Requirement("CC-PRC-001")]
    [InlineData("US", "USD")]
    [InlineData("ES", "EUR")]
    [InlineData("MX", "MXN")]
    [InlineData("DE", "EUR")]
    [InlineData("JP", "JPY")]
    [InlineData("IN", "INR")]
    public void Each_market_price_is_denominated_in_exactly_its_launch_currency(string marketCode, string currencyCode)
    {
        var price = Price(marketCode, 14_900);

        Assert.Equal(currencyCode, price.UnitPrice.Currency.Code);
        Assert.Equal(LaunchMarketCurrencies.CurrencyOf(Market.Parse(marketCode)), price.UnitPrice.Currency);
    }

    [Theory]
    [Requirement("CC-PRC-001")]
    [InlineData("US", "EUR")]
    [InlineData("ES", "USD")]
    [InlineData("MX", "USD")]
    [InlineData("DE", "USD")]
    [InlineData("JP", "INR")]
    [InlineData("IN", "JPY")]
    public void A_price_in_any_other_currency_is_rejected_at_construction(string marketCode, string wrongCurrencyCode)
    {
        var market = Market.Parse(marketCode);
        var wrongCurrency = Currency.Parse(wrongCurrencyCode);

        var exception = Assert.Throws<PricingValidationException>(
            () => new MarketPrice(Brisket, market, Money.FromMinorUnits(14_900, wrongCurrency), 1_000));

        Assert.Contains("CC-PRC-001", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-PRC-001")]
    [Requirement("CC-QA-004")]
    public void Jpy_prices_honor_zero_decimal_minor_units()
    {
        // ¥14,900 is 14900 minor units — the yen has no subdivision.
        var price = new MarketPrice(Brisket, Market.JP, Money.FromDecimal(14_900m, Currency.Jpy));
        Assert.Equal(14_900, price.UnitPrice.MinorUnits);

        // Sub-yen amounts are rejected outright, never rounded into acceptance.
        Assert.Throws<InvalidMoneyException>(() => Money.FromDecimal(149.50m, Currency.Jpy));
    }

    [Theory]
    [Requirement("CC-PRC-001")]
    [InlineData(0)]
    [InlineData(-14_900)]
    public void Non_positive_prices_are_rejected(long minorUnits)
    {
        Assert.Throws<PricingValidationException>(
            () => new MarketPrice(Brisket, Market.US, Money.FromMinorUnits(minorUnits, Currency.Usd)));
    }

    [Fact]
    [Requirement("CC-PRC-002")]
    public void A_de_price_must_carry_the_net_weight_for_unit_price_per_kilogram_derivation()
    {
        // Preisangabenverordnung: DE displays €/kg alongside every price; a DE
        // price without its derivation basis fails closed at construction.
        Assert.Throws<PricingValidationException>(
            () => new MarketPrice(Brisket, Market.DE, Money.FromMinorUnits(14_900, Currency.Eur)));

        var withWeight = new MarketPrice(Brisket, Market.DE, Money.FromMinorUnits(14_900, Currency.Eur), 6_000);
        Assert.Equal(6_000, withWeight.NetWeightGrams);
    }

    [Theory]
    [Requirement("CC-PRC-002")]
    [InlineData(0L)]
    [InlineData(-500L)]
    public void Non_positive_net_weights_are_rejected(long grams)
    {
        Assert.Throws<PricingValidationException>(
            () => new MarketPrice(Brisket, Market.DE, Money.FromMinorUnits(14_900, Currency.Eur), grams));
    }

    [Fact]
    [Requirement("CC-PRC-001")]
    public void Lookup_in_a_market_without_a_price_is_a_typed_miss_never_an_fx_conversion()
    {
        // The SKU is priced in the US only. No DE price exists, and no runtime
        // FX conversion may synthesize one (CC-PRC-001 negative case, AC-03).
        var priceList = new PriceList([Price("US", 14_900)]);

        var hit = priceList.Lookup(Brisket, Market.US);
        Assert.True(hit.IsPriced);
        Assert.Equal(14_900, hit.Price.UnitPrice.MinorUnits);

        var miss = priceList.Lookup(Brisket, Market.DE);
        Assert.False(miss.IsPriced);
        Assert.Throws<PriceUnavailableException>(() => miss.Price);
    }

    [Fact]
    [Requirement("CC-PRC-001")]
    public void Unknown_sku_lookup_is_a_typed_miss()
    {
        var priceList = new PriceList([Price("US", 14_900)]);

        var miss = priceList.Lookup(Paneer, Market.US);

        Assert.False(miss.IsPriced);
        Assert.Throws<PriceUnavailableException>(() => miss.Price);
    }

    [Fact]
    [Requirement("CC-PRC-001")]
    public void Duplicate_sku_market_rows_are_rejected()
    {
        Assert.Throws<PricingValidationException>(
            () => new PriceList([Price("US", 14_900), Price("US", 9_900)]));
    }

    [Fact]
    [Requirement("CC-PRC-001")]
    public void Uninitialized_market_or_sku_is_rejected_never_defaulted()
    {
        var priceList = new PriceList([Price("US", 14_900)]);

        Assert.Throws<PricingValidationException>(() => priceList.Lookup(default, Market.US));
        Assert.Throws<PricingValidationException>(() => priceList.Lookup(Brisket, default));
        Assert.Throws<PricingValidationException>(() => LaunchMarketCurrencies.CurrencyOf(default));
    }

    [Fact]
    [Requirement("CC-PRC-003")]
    [Requirement("CC-QA-004")]
    public void Attacker_scale_quantity_multiplication_fails_closed_instead_of_wrapping()
    {
        var price = Price("IN", 124_900_000); // ₹12,49,000.00 — INR large-value path

        Assert.Throws<MoneyOverflowException>(() => price.UnitPrice * long.MaxValue);
    }
}
