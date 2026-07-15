using CacheCow.Modules.PricingPromotions.Pricing;
using CacheCow.Modules.PricingPromotions.Rounding;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.PricingPromotions.Tests;

/// <summary>
/// Issue 032/034: the DE unit price per kilogram is derived data — exact
/// 128-bit integer arithmetic, with any rounding resolved only by an explicit,
/// caller-supplied mode because no rounding policy is ratified (issue 034,
/// Open Questions).
/// </summary>
public sealed class UnitPricePerKilogramTests
{
    private static readonly SkuId Brisket = SkuId.Parse("SKU-BRISKET-01");

    private static MarketPrice DePrice(long minorUnits, long grams) =>
        new(Brisket, Market.DE, Money.FromMinorUnits(minorUnits, Currency.Eur), grams);

    [Theory]
    [Requirement("CC-PRC-002")]
    [Requirement("CC-PRC-003")]
    [InlineData(RoundingMode.HalfAwayFromZero)]
    [InlineData(RoundingMode.HalfToEven)]
    [InlineData(RoundingMode.TowardZero)]
    [InlineData(RoundingMode.AwayFromZero)]
    public void Exact_divisions_are_identical_under_every_rounding_mode(RoundingMode mode)
    {
        // €10.00 for 500 g is exactly €20.00/kg; no rounding is involved.
        var perKilogram = DePrice(1_000, 500).UnitPricePerKilogram(mode);

        Assert.Equal(2_000, perKilogram.MinorUnits);
        Assert.Equal(Currency.Eur, perKilogram.Currency);
    }

    [Theory]
    [Requirement("CC-PRC-002")]
    [Requirement("CC-QA-004")]
    [InlineData(RoundingMode.HalfAwayFromZero, 2_483)]
    [InlineData(RoundingMode.HalfToEven, 2_483)]
    [InlineData(RoundingMode.TowardZero, 2_483)]
    [InlineData(RoundingMode.AwayFromZero, 2_484)]
    public void Inexact_divisions_follow_the_explicit_rounding_mode(RoundingMode mode, long expectedMinorUnits)
    {
        // €149.00 for 6 kg: exactly 2483.33… cents/kg.
        Assert.Equal(expectedMinorUnits, DePrice(14_900, 6_000).UnitPricePerKilogram(mode).MinorUnits);
    }

    [Theory]
    [Requirement("CC-PRC-002")]
    [Requirement("CC-QA-004")]
    [InlineData(RoundingMode.HalfAwayFromZero, 3)]
    [InlineData(RoundingMode.HalfToEven, 2)]
    [InlineData(RoundingMode.TowardZero, 2)]
    [InlineData(RoundingMode.AwayFromZero, 3)]
    public void Midpoints_are_resolved_by_the_explicit_mode_only(RoundingMode mode, long expectedMinorUnits)
    {
        // 1 cent for 400 g: exactly 2.5 cents/kg — a true midpoint.
        Assert.Equal(expectedMinorUnits, DePrice(1, 400).UnitPricePerKilogram(mode).MinorUnits);
    }

    [Fact]
    [Requirement("CC-PRC-002")]
    public void An_unspecified_rounding_mode_is_rejected_because_no_policy_is_ratified()
    {
        var exception = Assert.Throws<PricingValidationException>(
            () => DePrice(14_900, 6_000).UnitPricePerKilogram(default));

        Assert.Contains("Open Questions", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-PRC-002")]
    public void Deriving_without_a_net_weight_fails_closed()
    {
        var usPrice = new MarketPrice(Brisket, Market.US, Money.FromMinorUnits(14_900, Currency.Usd));

        Assert.Throws<PricingValidationException>(
            () => usPrice.UnitPricePerKilogram(RoundingMode.HalfAwayFromZero));
    }

    [Fact]
    [Requirement("CC-PRC-003")]
    [Requirement("CC-QA-004")]
    public void Jpy_zero_decimal_derivation_stays_in_whole_yen()
    {
        var jpPrice = new MarketPrice(
            Brisket, Market.JP, Money.FromMinorUnits(14_900, Currency.Jpy), 2_000);

        var perKilogram = jpPrice.UnitPricePerKilogram(RoundingMode.HalfToEven);

        Assert.Equal(7_450, perKilogram.MinorUnits);
        Assert.Equal(Currency.Jpy, perKilogram.Currency);
    }

    [Fact]
    [Requirement("CC-PRC-003")]
    public void Derivation_overflow_fails_closed_instead_of_wrapping()
    {
        // long.MaxValue cents over 1 g scales by 1000: outside the checked range.
        Assert.Throws<MoneyOverflowException>(
            () => DePrice(long.MaxValue, 1).UnitPricePerKilogram(RoundingMode.TowardZero));
    }
}
