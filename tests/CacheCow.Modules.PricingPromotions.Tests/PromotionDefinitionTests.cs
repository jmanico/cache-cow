using System.Globalization;
using CacheCow.Modules.PricingPromotions.Promotions;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.PricingPromotions.Tests;

/// <summary>
/// Issue 033 AC-07: promotion-administration validation — invalid windows,
/// amounts, scopes, and currencies are rejected at construction, and
/// non-default stacking stays unrepresentable pending ratification.
/// </summary>
public sealed class PromotionDefinitionTests
{
    private static readonly SkuId Brisket = SkuId.Parse("SKU-BRISKET-01");

    private static DateTime MarketTime(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture);

    private static Promotion Build(
        DateTime start, DateTime end, Discount? discount = null, StackingPolicy stacking = StackingPolicy.NoStacking) =>
        new("promo", Market.US, discount ?? new PercentageDiscount(1_000), PromotionScope.ForSku(Brisket), start, end, stacking: stacking);

    [Fact]
    [Requirement("CC-PRC-006")]
    public void An_end_before_or_equal_to_start_window_is_rejected()
    {
        var start = MarketTime("2026-07-01T00:00:00");

        Assert.Throws<PricingValidationException>(() => Build(start, start));
        Assert.Throws<PricingValidationException>(() => Build(start, MarketTime("2026-06-01T00:00:00")));
    }

    [Fact]
    [Requirement("CC-PRC-006")]
    public void Window_timestamps_must_be_market_wall_clock_not_utc_or_machine_local()
    {
        var start = MarketTime("2026-07-01T00:00:00");
        var end = MarketTime("2026-08-01T00:00:00");

        Assert.Throws<PricingValidationException>(
            () => Build(DateTime.SpecifyKind(start, DateTimeKind.Utc), end));
        Assert.Throws<PricingValidationException>(
            () => Build(start, DateTime.SpecifyKind(end, DateTimeKind.Local)));
    }

    [Theory]
    [Requirement("CC-PRC-006")]
    [InlineData(0L)]
    [InlineData(-100L)]
    [InlineData(10_001L)]
    public void Percentage_discounts_outside_zero_to_one_hundred_percent_are_rejected(long basisPoints)
    {
        Assert.Throws<PricingValidationException>(() => new PercentageDiscount(basisPoints));
    }

    [Theory]
    [Requirement("CC-PRC-006")]
    [InlineData(0L)]
    [InlineData(-200L)]
    public void Non_positive_fixed_discounts_are_rejected(long minorUnits)
    {
        Assert.Throws<PricingValidationException>(
            () => new FixedAmountPerUnitDiscount(Money.FromMinorUnits(minorUnits, Currency.Usd)));
    }

    [Fact]
    [Requirement("CC-PRC-001")]
    [Requirement("CC-PRC-006")]
    public void A_fixed_discount_in_another_markets_currency_is_rejected()
    {
        // A JP promotion cannot carry a EUR discount: promotions move money in
        // exactly the market's currency, and no FX conversion exists.
        var discount = new FixedAmountPerUnitDiscount(Money.FromMinorUnits(200, Currency.Eur));

        var exception = Assert.Throws<PricingValidationException>(
            () => new Promotion(
                "promo", Market.JP, discount, PromotionScope.ForSku(Brisket),
                MarketTime("2026-07-01T00:00:00"), MarketTime("2026-08-01T00:00:00")));

        Assert.Contains("CC-PRC-001", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [Requirement("CC-PRC-006")]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_promotion_id_is_rejected(string id)
    {
        Assert.Throws<PricingValidationException>(
            () => new Promotion(
                id, Market.US, new PercentageDiscount(1_000), PromotionScope.ForSku(Brisket),
                MarketTime("2026-07-01T00:00:00"), MarketTime("2026-08-01T00:00:00")));
    }

    [Fact]
    [Requirement("CC-PRC-006")]
    public void Scope_targets_must_be_concrete()
    {
        Assert.Throws<PricingValidationException>(() => PromotionScope.ForSku(default));
        Assert.Throws<PricingValidationException>(() => PromotionScope.ForCategory(""));
        Assert.Throws<PricingValidationException>(() => PromotionScope.ForCategory("   "));
    }

    [Fact]
    [Requirement("CC-PRC-006")]
    public void Non_default_stacking_is_unrepresentable_pending_ratification()
    {
        // CC-PRC-006 ratifies only the no-stacking default; any other value is
        // rejected until a human ratifies non-default stacking semantics
        // (issue 033, Open Questions — flagged, not resolved).
        var exception = Assert.Throws<PricingValidationException>(
            () => Build(
                MarketTime("2026-07-01T00:00:00"), MarketTime("2026-08-01T00:00:00"),
                stacking: (StackingPolicy)1));

        Assert.Contains("ratification", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [Requirement("CC-PRC-003")]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void Non_positive_line_quantities_are_rejected(long quantity)
    {
        Assert.Throws<PricingValidationException>(
            () => new PromotionLine(Brisket, Money.FromMinorUnits(14_900, Currency.Usd), quantity));
    }

    [Fact]
    [Requirement("CC-PRC-006")]
    public void An_unknown_timezone_identifier_is_rejected_at_configuration_time()
    {
        Assert.Throws<PricingValidationException>(
            () => new MarketTimeZoneMap(new Dictionary<Market, string> { [Market.JP] = "Not/AZone" }));
    }
}
