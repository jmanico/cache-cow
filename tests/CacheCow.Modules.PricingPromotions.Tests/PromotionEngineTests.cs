using System.Globalization;
using CacheCow.Modules.PricingPromotions.Pricing;
using CacheCow.Modules.PricingPromotions.Promotions;
using CacheCow.Modules.PricingPromotions.Rounding;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.PricingPromotions.Tests;

/// <summary>
/// Issue 033: promotion evaluation — market isolation, scope, market-timezone
/// window boundaries against the injected authoritative clock, no stacking,
/// expired-promotion rejection, negative-total and overflow guards
/// (CC-PRC-006, CC-PRC-003, CC-QA-004).
/// </summary>
public sealed class PromotionEngineTests
{
    private static readonly SkuId Brisket = SkuId.Parse("SKU-BRISKET-01");
    private static readonly SkuId Ribs = SkuId.Parse("SKU-RIBS-01");

    // Test-supplied mapping only: the canonical per-market IANA timezone is an
    // unratified open decision (issue 033, Open Questions; ARCHITECTURE.md
    // pattern — never guessed in production code).
    private static readonly MarketTimeZoneMap TestZones = new(new Dictionary<Market, string>
    {
        [Market.US] = "America/Chicago",
        [Market.ES] = "Europe/Madrid",
        [Market.MX] = "America/Mexico_City",
        [Market.DE] = "Europe/Berlin",
        [Market.JP] = "Asia/Tokyo",
        [Market.IN] = "Asia/Kolkata",
    });

    private static DateTime MarketTime(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture);

    private static DateTimeOffset Utc(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);

    private static Promotion JulyPromotion(
        string id, Market market, Discount discount, PromotionScope scope, bool isClearance = false) =>
        new(id, market, discount, scope,
            MarketTime("2026-07-01T00:00:00"), MarketTime("2026-08-01T00:00:00"), isClearance);

    private static PromotionEvaluationResult Evaluate(
        PromotionEvaluationRequest request, DateTimeOffset utcNow, IMarketTimeZoneProvider? zones = null) =>
        new PromotionEvaluator(zones ?? TestZones, new FixedTimeProvider(utcNow)).Evaluate(request);

    private static PromotionLine Line(Market market, long unitMinorUnits, long quantity, string? category = null) =>
        new(Brisket, Money.FromMinorUnits(unitMinorUnits, LaunchMarketCurrencies.CurrencyOf(market)), quantity, category);

    [Fact]
    [Requirement("CC-PRC-006")]
    public void Percentage_discount_applies_inside_its_window()
    {
        var promotion = JulyPromotion("summer-10", Market.JP, new PercentageDiscount(1_000), PromotionScope.ForSku(Brisket));
        var request = new PromotionEvaluationRequest(
            Market.JP, [Line(Market.JP, 14_900, 2)], [promotion], RoundingMode.HalfToEven);

        var result = Evaluate(request, Utc("2026-07-15T03:00:00+00:00"));

        var line = Assert.Single(result.Lines);
        Assert.Equal(29_800, line.Subtotal.MinorUnits);
        Assert.Equal(2_980, line.Discount.MinorUnits);
        Assert.Equal(26_820, line.Total.MinorUnits);
        Assert.Equal("summer-10", line.AppliedPromotionId);
        Assert.Equal(26_820, result.Total.MinorUnits);
        Assert.Equal(Currency.Jpy, result.Total.Currency);
    }

    [Fact]
    [Requirement("CC-PRC-006")]
    public void Fixed_per_unit_discount_multiplies_by_quantity()
    {
        var promotion = JulyPromotion(
            "two-dollars-off", Market.US,
            new FixedAmountPerUnitDiscount(Money.FromMinorUnits(200, Currency.Usd)),
            PromotionScope.ForSku(Brisket));
        var request = new PromotionEvaluationRequest(
            Market.US, [Line(Market.US, 14_900, 3)], [promotion], RoundingMode.HalfToEven);

        var result = Evaluate(request, Utc("2026-07-15T12:00:00+00:00"));

        var line = Assert.Single(result.Lines);
        Assert.Equal(44_700, line.Subtotal.MinorUnits);
        Assert.Equal(600, line.Discount.MinorUnits);
        Assert.Equal(44_100, line.Total.MinorUnits);
    }

    [Fact]
    [Requirement("CC-PRC-006")]
    public void Per_sku_scope_touches_only_that_sku()
    {
        var promotion = JulyPromotion("brisket-only", Market.US, new PercentageDiscount(1_000), PromotionScope.ForSku(Brisket));
        var otherLine = new PromotionLine(Ribs, Money.FromMinorUnits(9_900, Currency.Usd), 1);
        var request = new PromotionEvaluationRequest(
            Market.US, [Line(Market.US, 14_900, 1), otherLine], [promotion], RoundingMode.HalfToEven);

        var result = Evaluate(request, Utc("2026-07-15T12:00:00+00:00"));

        Assert.Equal(1_490, result.Lines[0].Discount.MinorUnits);
        Assert.Equal(0, result.Lines[1].Discount.MinorUnits);
        Assert.Null(result.Lines[1].AppliedPromotionId);
    }

    [Fact]
    [Requirement("CC-PRC-006")]
    public void Per_category_scope_matches_lines_by_category()
    {
        var promotion = JulyPromotion("smoked-cuts", Market.US, new PercentageDiscount(1_000), PromotionScope.ForCategory("smoked-cuts"));
        var inCategory = Line(Market.US, 14_900, 1, "smoked-cuts");
        var outsideCategory = new PromotionLine(Ribs, Money.FromMinorUnits(9_900, Currency.Usd), 1, "sides");
        var uncategorized = new PromotionLine(SkuId.Parse("SKU-SAUCE-01"), Money.FromMinorUnits(900, Currency.Usd), 1);
        var request = new PromotionEvaluationRequest(
            Market.US, [inCategory, outsideCategory, uncategorized], [promotion], RoundingMode.HalfToEven);

        var result = Evaluate(request, Utc("2026-07-15T12:00:00+00:00"));

        Assert.Equal("smoked-cuts", result.Lines[0].AppliedPromotionId);
        Assert.Null(result.Lines[1].AppliedPromotionId);
        Assert.Null(result.Lines[2].AppliedPromotionId);
    }

    [Fact]
    [Requirement("CC-PRC-006")]
    public void A_promotion_applies_only_in_its_own_market()
    {
        var usPromotion = JulyPromotion("us-only", Market.US, new PercentageDiscount(1_000), PromotionScope.ForSku(Brisket));
        var request = new PromotionEvaluationRequest(
            Market.DE, [Line(Market.DE, 14_900, 1)], [usPromotion], RoundingMode.HalfToEven);

        var result = Evaluate(request, Utc("2026-07-15T12:00:00+00:00"));

        Assert.Equal(0, result.Lines[0].Discount.MinorUnits);
        Assert.Null(result.Lines[0].AppliedPromotionId);
    }

    [Theory]
    [Requirement("CC-PRC-006")]
    [Requirement("CC-QA-004")]
    [InlineData("2026-06-30T14:59:59+00:00", false)] // 23:59:59 JST June 30 — before the window
    [InlineData("2026-06-30T15:00:00+00:00", true)]  // exactly 2026-07-01 00:00:00 JST — start is inclusive
    [InlineData("2026-07-31T14:59:59+00:00", true)]  // 23:59:59 JST July 31 — last active second
    [InlineData("2026-07-31T15:00:00+00:00", false)] // exactly 2026-08-01 00:00:00 JST — stops at that instant
    public void Window_boundaries_are_interpreted_in_the_market_timezone_not_utc(string utcInstant, bool active)
    {
        // Window authored as JP wall-clock time: 2026-07-01 00:00 to 2026-08-01 00:00 JST.
        var promotion = JulyPromotion("jst-window", Market.JP, new PercentageDiscount(1_000), PromotionScope.ForSku(Brisket));
        var request = new PromotionEvaluationRequest(
            Market.JP, [Line(Market.JP, 14_900, 1)], [promotion], RoundingMode.HalfToEven);

        var result = Evaluate(request, Utc(utcInstant));

        Assert.Equal(active ? "jst-window" : null, result.Lines[0].AppliedPromotionId);
        Assert.Equal(active ? 1_490 : 0, result.Lines[0].Discount.MinorUnits);
    }

    [Theory]
    [Requirement("CC-PRC-006")]
    [Requirement("CC-QA-004")]
    [InlineData("2026-07-31T14:58:59+00:00", true)]  // 23:58:59 JST — still inside
    [InlineData("2026-07-31T14:59:00+00:00", false)] // 23:59:00 JST — the promo ended at 23:59 market time
    public void A_promotion_ending_2359_market_time_is_evaluated_in_market_time_from_any_zone(string utcInstant, bool active)
    {
        var promotion = new Promotion(
            "ends-2359", Market.JP, new PercentageDiscount(1_000), PromotionScope.ForSku(Brisket),
            MarketTime("2026-07-01T00:00:00"), MarketTime("2026-07-31T23:59:00"));
        var request = new PromotionEvaluationRequest(
            Market.JP, [Line(Market.JP, 14_900, 1)], [promotion], RoundingMode.HalfToEven);

        var result = Evaluate(request, Utc(utcInstant));

        Assert.Equal(active ? "ends-2359" : null, result.Lines[0].AppliedPromotionId);
    }

    [Theory]
    [Requirement("CC-PRC-006")]
    [Requirement("CC-QA-004")]
    [InlineData("2026-07-31T21:59:59+00:00", true)]  // 23:59:59 CEST (UTC+2, summer time)
    [InlineData("2026-07-31T22:00:00+00:00", false)] // 2026-08-01 00:00:00 CEST
    public void De_windows_respect_summer_time_offsets(string utcInstant, bool active)
    {
        var promotion = JulyPromotion("de-window", Market.DE, new PercentageDiscount(1_000), PromotionScope.ForSku(Brisket));
        var request = new PromotionEvaluationRequest(
            Market.DE, [Line(Market.DE, 14_900, 1)], [promotion], RoundingMode.HalfToEven);

        var result = Evaluate(request, Utc(utcInstant));

        Assert.Equal(active ? "de-window" : null, result.Lines[0].AppliedPromotionId);
    }

    [Fact]
    [Requirement("CC-PRC-006")]
    public void An_expired_promotion_replayed_from_cached_ui_never_applies()
    {
        // The client still "sees" the promotion in cached UI and submits it;
        // the authoritative clock says it expired — no expired discount ever
        // reaches the total (CC-PRC-006 AC-03).
        var promotion = JulyPromotion("expired", Market.JP, new PercentageDiscount(5_000), PromotionScope.ForSku(Brisket));
        var request = new PromotionEvaluationRequest(
            Market.JP, [Line(Market.JP, 14_900, 1)], [promotion], RoundingMode.HalfToEven);

        var result = Evaluate(request, Utc("2026-09-01T00:00:00+00:00"));

        Assert.Equal(0, result.Lines[0].Discount.MinorUnits);
        Assert.Null(result.Lines[0].AppliedPromotionId);
        Assert.Equal(14_900, result.Total.MinorUnits);
    }

    [Fact]
    [Requirement("CC-PRC-006")]
    public void A_not_yet_started_promotion_does_not_apply()
    {
        var promotion = JulyPromotion("future", Market.JP, new PercentageDiscount(5_000), PromotionScope.ForSku(Brisket));
        var request = new PromotionEvaluationRequest(
            Market.JP, [Line(Market.JP, 14_900, 1)], [promotion], RoundingMode.HalfToEven);

        var result = Evaluate(request, Utc("2026-05-01T00:00:00+00:00"));

        Assert.Null(result.Lines[0].AppliedPromotionId);
    }

    [Fact]
    [Requirement("CC-PRC-006")]
    public void No_stacking_exactly_one_promotion_applies_per_line()
    {
        var percentage = JulyPromotion("ten-percent", Market.US, new PercentageDiscount(1_000), PromotionScope.ForSku(Brisket));
        var fixedAmount = JulyPromotion(
            "one-dollar-off", Market.US,
            new FixedAmountPerUnitDiscount(Money.FromMinorUnits(100, Currency.Usd)),
            PromotionScope.ForSku(Brisket));
        var request = new PromotionEvaluationRequest(
            Market.US, [Line(Market.US, 14_900, 1)], [percentage, fixedAmount], RoundingMode.HalfToEven);

        var result = Evaluate(request, Utc("2026-07-15T12:00:00+00:00"));

        // Exactly one applied: 10% (1490) — never 1490 + 100 stacked.
        var line = Assert.Single(result.Lines);
        Assert.Equal("ten-percent", line.AppliedPromotionId);
        Assert.Equal(1_490, line.Discount.MinorUnits);
        Assert.Equal(13_410, line.Total.MinorUnits);
    }

    [Fact]
    [Requirement("CC-PRC-006")]
    public void Equal_discounts_tie_break_deterministically_by_ordinal_id()
    {
        // Selection among equally applicable promotions is unratified (issue
        // 033, Open Questions); the engine's derived rule is deterministic:
        // greatest discount, ties by ordinal promotion ID.
        var second = JulyPromotion("promo-b", Market.US, new PercentageDiscount(1_000), PromotionScope.ForSku(Brisket));
        var first = JulyPromotion("promo-a", Market.US, new PercentageDiscount(1_000), PromotionScope.ForSku(Brisket));
        var request = new PromotionEvaluationRequest(
            Market.US, [Line(Market.US, 14_900, 1)], [second, first], RoundingMode.HalfToEven);

        var result = Evaluate(request, Utc("2026-07-15T12:00:00+00:00"));

        Assert.Equal("promo-a", result.Lines[0].AppliedPromotionId);
    }

    [Fact]
    [Requirement("CC-PRC-003")]
    [Requirement("CC-PRC-006")]
    public void A_discount_never_drives_a_line_or_total_negative()
    {
        var oversized = JulyPromotion(
            "oversized", Market.US,
            new FixedAmountPerUnitDiscount(Money.FromMinorUnits(20_000, Currency.Usd)),
            PromotionScope.ForSku(Brisket));
        var request = new PromotionEvaluationRequest(
            Market.US, [Line(Market.US, 14_900, 1)], [oversized], RoundingMode.HalfToEven);

        var result = Evaluate(request, Utc("2026-07-15T12:00:00+00:00"));

        var line = Assert.Single(result.Lines);
        Assert.Equal(14_900, line.Discount.MinorUnits); // clamped to the subtotal
        Assert.Equal(0, line.Total.MinorUnits);
        Assert.Equal(0, result.Total.MinorUnits);
    }

    [Fact]
    [Requirement("CC-PRC-006")]
    public void A_full_percentage_discount_reaches_exactly_zero()
    {
        var promotion = JulyPromotion("full", Market.US, new PercentageDiscount(10_000), PromotionScope.ForSku(Brisket));
        var request = new PromotionEvaluationRequest(
            Market.US, [Line(Market.US, 14_900, 2)], [promotion], RoundingMode.HalfToEven);

        var result = Evaluate(request, Utc("2026-07-15T12:00:00+00:00"));

        Assert.Equal(0, result.Total.MinorUnits);
    }

    [Fact]
    [Requirement("CC-PRC-003")]
    [Requirement("CC-QA-004")]
    public void Attacker_scale_quantity_fails_closed_at_subtotal_computation()
    {
        var request = new PromotionEvaluationRequest(
            Market.US, [Line(Market.US, 2, long.MaxValue)], [], RoundingMode.HalfToEven);

        Assert.Throws<MoneyOverflowException>(() => Evaluate(request, Utc("2026-07-15T12:00:00+00:00")));
    }

    [Fact]
    [Requirement("CC-PRC-003")]
    public void Fixed_discount_overflow_denies_the_discount_instead_of_wrapping()
    {
        // Subtotal fits; the fixed discount multiplication would overflow — the
        // evaluation fails closed and never grants a wrapped discount.
        var quantity = long.MaxValue / 10;
        var promotion = JulyPromotion(
            "overflowing", Market.US,
            new FixedAmountPerUnitDiscount(Money.FromMinorUnits(100, Currency.Usd)),
            PromotionScope.ForSku(Brisket));
        var request = new PromotionEvaluationRequest(
            Market.US, [Line(Market.US, 5, quantity)], [promotion], RoundingMode.HalfToEven);

        Assert.Throws<MoneyOverflowException>(() => Evaluate(request, Utc("2026-07-15T12:00:00+00:00")));
    }

    [Theory]
    [Requirement("CC-PRC-003")]
    [Requirement("CC-QA-004")]
    [InlineData("US", RoundingMode.HalfAwayFromZero, 3)]
    [InlineData("US", RoundingMode.HalfToEven, 2)]
    [InlineData("US", RoundingMode.TowardZero, 2)]
    [InlineData("US", RoundingMode.AwayFromZero, 3)]
    [InlineData("ES", RoundingMode.HalfAwayFromZero, 3)]
    [InlineData("ES", RoundingMode.HalfToEven, 2)]
    [InlineData("ES", RoundingMode.TowardZero, 2)]
    [InlineData("ES", RoundingMode.AwayFromZero, 3)]
    [InlineData("MX", RoundingMode.HalfAwayFromZero, 3)]
    [InlineData("MX", RoundingMode.HalfToEven, 2)]
    [InlineData("MX", RoundingMode.TowardZero, 2)]
    [InlineData("MX", RoundingMode.AwayFromZero, 3)]
    [InlineData("JP", RoundingMode.HalfAwayFromZero, 3)]
    [InlineData("JP", RoundingMode.HalfToEven, 2)]
    [InlineData("JP", RoundingMode.TowardZero, 2)]
    [InlineData("JP", RoundingMode.AwayFromZero, 3)]
    [InlineData("IN", RoundingMode.HalfAwayFromZero, 3)]
    [InlineData("IN", RoundingMode.HalfToEven, 2)]
    [InlineData("IN", RoundingMode.TowardZero, 2)]
    [InlineData("IN", RoundingMode.AwayFromZero, 3)]
    public void Percentage_rounding_follows_the_explicit_mode_in_every_launch_currency(
        string marketCode, RoundingMode mode, long expectedDiscountMinorUnits)
    {
        // 5% of 50 minor units is exactly 2.5 — a true midpoint in every currency
        // (cents, céntimos, centavos, yen, paise).
        var market = Market.Parse(marketCode);
        var promotion = JulyPromotion("five-percent", market, new PercentageDiscount(500), PromotionScope.ForSku(Brisket));
        var request = new PromotionEvaluationRequest(market, [Line(market, 50, 1)], [promotion], mode);

        var result = Evaluate(request, Utc("2026-07-15T12:00:00+00:00"));

        Assert.Equal(expectedDiscountMinorUnits, result.Lines[0].Discount.MinorUnits);
    }

    [Fact]
    [Requirement("CC-PRC-006")]
    public void A_promotion_whose_discount_rounds_to_zero_is_not_reported_as_applied()
    {
        // 0.01% of 10 minor units truncates to zero under TowardZero: nothing applied.
        var promotion = JulyPromotion("negligible", Market.US, new PercentageDiscount(1), PromotionScope.ForSku(Brisket));
        var request = new PromotionEvaluationRequest(
            Market.US, [Line(Market.US, 10, 1)], [promotion], RoundingMode.TowardZero);

        var result = Evaluate(request, Utc("2026-07-15T12:00:00+00:00"));

        Assert.Null(result.Lines[0].AppliedPromotionId);
        Assert.Equal(0, result.Lines[0].Discount.MinorUnits);
    }

    [Fact]
    [Requirement("CC-PRC-003")]
    public void An_unspecified_rounding_mode_is_rejected_because_no_policy_is_ratified()
    {
        var promotion = JulyPromotion("any", Market.US, new PercentageDiscount(1_000), PromotionScope.ForSku(Brisket));
        var request = new PromotionEvaluationRequest(
            Market.US, [Line(Market.US, 14_900, 1)], [promotion], default);

        Assert.Throws<PricingValidationException>(() => Evaluate(request, Utc("2026-07-15T12:00:00+00:00")));
    }

    [Fact]
    [Requirement("CC-PRC-006")]
    public void A_missing_market_timezone_mapping_fails_closed()
    {
        var emptyMap = new MarketTimeZoneMap(new Dictionary<Market, string>());
        var promotion = JulyPromotion("any", Market.JP, new PercentageDiscount(1_000), PromotionScope.ForSku(Brisket));
        var request = new PromotionEvaluationRequest(
            Market.JP, [Line(Market.JP, 14_900, 1)], [promotion], RoundingMode.HalfToEven);

        Assert.Throws<MarketTimeZoneUnavailableException>(
            () => Evaluate(request, Utc("2026-07-15T12:00:00+00:00"), emptyMap));
    }

    [Fact]
    [Requirement("CC-PRC-006")]
    public void Evaluation_without_candidate_promotions_needs_no_timezone_configuration()
    {
        var emptyMap = new MarketTimeZoneMap(new Dictionary<Market, string>());
        var request = new PromotionEvaluationRequest(
            Market.JP, [Line(Market.JP, 14_900, 1)], [], RoundingMode.HalfToEven);

        var result = Evaluate(request, Utc("2026-07-15T12:00:00+00:00"), emptyMap);

        Assert.Equal(14_900, result.Total.MinorUnits);
    }

    [Fact]
    [Requirement("CC-PRC-001")]
    public void A_line_priced_in_another_currency_is_rejected_never_converted()
    {
        var request = new PromotionEvaluationRequest(
            Market.US, [Line(Market.DE, 14_900, 1)], [], RoundingMode.HalfToEven);

        var exception = Assert.Throws<PricingValidationException>(
            () => Evaluate(request, Utc("2026-07-15T12:00:00+00:00")));

        Assert.Contains("no runtime FX conversion", exception.Message, StringComparison.Ordinal);
    }
}
