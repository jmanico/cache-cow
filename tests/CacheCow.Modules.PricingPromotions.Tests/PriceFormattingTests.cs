using CacheCow.Modules.PricingPromotions.Formatting;
using CacheCow.Modules.PricingPromotions.Pricing;
using CacheCow.Modules.PricingPromotions.Rounding;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.PricingPromotions.Tests;

/// <summary>
/// Issue 034: locale-aware server-side price formatting (CC-PRC-004,
/// CC-I18N-003) reproducing the DESIGN.md §4.4 worked examples, plus the market
/// tax-display convention consumed as structured input (CC-PRC-002).
/// Note on ja-JP: DESIGN.md §4.4 prints the fullwidth yen sign ￥ (U+FFE5);
/// .NET/ICU CLDR data emits the halfwidth ¥ (U+00A5). The tests assert the
/// locale-library output — hand-adjusting the glyph would be hand-formatting
/// (CC-PRC-004). Divergence flagged for design review.
/// </summary>
public sealed class PriceFormattingTests
{
    private static Money Of(long minorUnits, Currency currency) => Money.FromMinorUnits(minorUnits, currency);

    [Theory]
    [Requirement("CC-PRC-004")]
    [Requirement("CC-I18N-003")]
    [Requirement("CC-QA-004")]
    [InlineData("en-US", "USD", 14_900L, "$149.00")]
    [InlineData("es-ES", "EUR", 14_900L, "149,00 €")]
    [InlineData("de-DE", "EUR", 14_900L, "149,00 €")]
    [InlineData("ja-JP", "JPY", 14_900L, "¥14,900")] // DESIGN.md shows U+FFE5 ￥; ICU emits U+00A5 ¥ — flagged divergence
    [InlineData("hi-IN", "INR", 124_900_000L, "₹12,49,000.00")]
    [InlineData("en-IN", "INR", 124_900_000L, "₹12,49,000.00")]
    [InlineData("es-MX", "MXN", 14_900L, "$149.00")]
    public void Formatting_reproduces_the_design_worked_examples_from_locale_data(
        string localeTag, string currencyCode, long minorUnits, string expected)
    {
        var formatted = PriceFormatter.FormatAmount(
            Of(minorUnits, Currency.Parse(currencyCode)), Locale.Parse(localeTag));

        Assert.Equal(expected, formatted);
    }

    [Fact]
    [Requirement("CC-PRC-004")]
    [Requirement("CC-QA-004")]
    public void Inr_lakh_crore_grouping_comes_from_the_locale_never_hand_grouping()
    {
        // ₹12,49,000.00 — 3;2 grouping straight from CLDR (DESIGN.md §4.4).
        var formatted = PriceFormatter.FormatAmount(Of(124_900_000, Currency.Inr), Locale.Parse("hi-IN"));

        Assert.Equal("₹12,49,000.00", formatted);
    }

    [Fact]
    [Requirement("CC-PRC-004")]
    [Requirement("CC-QA-004")]
    public void Jpy_renders_zero_decimals_in_every_locale()
    {
        var yen = Of(14_900, Currency.Jpy);

        Assert.Equal("¥14,900", PriceFormatter.FormatAmount(yen, Locale.Parse("ja-JP")));
        // German grouping, still whole yen and the yen's own symbol.
        Assert.Equal("14.900 ¥", PriceFormatter.FormatAmount(yen, Locale.Parse("de-DE")));
    }

    [Fact]
    [Requirement("CC-PRC-004")]
    public void Mxn_disambiguation_appends_the_iso_code_as_an_explicit_option()
    {
        // DESIGN.md §4.4: "$149.00 MXN" where cross-currency ambiguity exists.
        // The trigger condition is unspecified (issue 034, Open Questions), so
        // callers opt in explicitly.
        var options = new PriceFormatOptions(CurrencyDisambiguation.AppendIsoCurrencyCode);

        Assert.Equal(
            "$149.00 MXN",
            PriceFormatter.FormatAmount(Of(14_900, Currency.Mxn), Locale.Parse("es-MX"), options));
    }

    [Theory]
    [Requirement("CC-I18N-003")]
    [Requirement("CC-PRC-004")]
    [InlineData("en-US", "EUR", 14_900L, "€149.00")]      // US formatting, still euros
    [InlineData("hi-IN", "USD", 124_900_000L, "$12,49,000.00")] // lakh/crore grouping, still dollars
    [InlineData("ja-JP", "EUR", 14_900L, "€149.00")]
    public void Locale_changes_formatting_only_currency_and_amount_come_from_the_money_value(
        string localeTag, string currencyCode, long minorUnits, string expected)
    {
        // CC-SEC-012 adjacent: a client-chosen locale can restyle a price but
        // can never change its currency or amount.
        var formatted = PriceFormatter.FormatAmount(
            Of(minorUnits, Currency.Parse(currencyCode)), Locale.Parse(localeTag));

        Assert.Equal(expected, formatted);
    }

    [Fact]
    [Requirement("CC-PRC-004")]
    public void A_locale_without_culture_data_is_rejected_not_hand_formatted()
    {
        // Well-formed BCP 47, no ICU culture behind it: fail closed.
        Assert.Throws<PricingValidationException>(
            () => PriceFormatter.FormatAmount(Of(14_900, Currency.Usd), Locale.Parse("xx-XX")));
    }

    [Theory]
    [Requirement("CC-PRC-002")]
    [InlineData(TaxPresentation.TaxInclusive, PriceDisplayNote.TaxInclusive)]
    [InlineData(TaxPresentation.TaxExclusiveEstimatedAtCheckout, PriceDisplayNote.TaxExclusiveEstimatedTaxAtCheckout)]
    public void The_tax_convention_note_is_emitted_as_a_structured_key_not_a_sentence(
        TaxPresentation presentation, PriceDisplayNote expectedNote)
    {
        var result = PriceFormatter.FormatPrice(
            Of(14_900, Currency.Usd), Locale.Parse("en-US"),
            new TaxDisplayContext(presentation, displayUnitPricePerKilogram: false));

        Assert.Equal("$149.00", result.Amount);
        Assert.Null(result.UnitPricePerKilogramAmount);
        Assert.Equal([expectedNote], result.Notes);
    }

    [Fact]
    [Requirement("CC-PRC-002")]
    [Requirement("CC-QA-004")]
    public void De_prices_carry_the_locale_formatted_unit_price_per_kilogram()
    {
        // End-to-end across issues 032 + 034: €149.00 for 6 kg, derived with an
        // explicit rounding mode, displayed per Preisangabenverordnung.
        var price = new MarketPrice(
            SkuId.Parse("SKU-BRISKET-01"), Market.DE, Of(14_900, Currency.Eur), 6_000);
        var perKilogram = price.UnitPricePerKilogram(RoundingMode.HalfAwayFromZero);

        var result = PriceFormatter.FormatPrice(
            price.UnitPrice, Locale.Parse("de-DE"),
            new TaxDisplayContext(TaxPresentation.TaxInclusive, displayUnitPricePerKilogram: true),
            perKilogram);

        Assert.Equal("149,00 €", result.Amount);
        Assert.Equal("24,83 €", result.UnitPricePerKilogramAmount);
        Assert.Equal([PriceDisplayNote.TaxInclusive, PriceDisplayNote.UnitPricePerKilogram], result.Notes);
    }

    [Fact]
    [Requirement("CC-PRC-002")]
    public void A_de_convention_price_without_its_unit_price_fails_closed()
    {
        Assert.Throws<PricingValidationException>(
            () => PriceFormatter.FormatPrice(
                Of(14_900, Currency.Eur), Locale.Parse("de-DE"),
                new TaxDisplayContext(TaxPresentation.TaxInclusive, displayUnitPricePerKilogram: true)));
    }

    [Fact]
    [Requirement("CC-PRC-002")]
    public void A_unit_price_in_another_currency_is_rejected()
    {
        Assert.Throws<PricingValidationException>(
            () => PriceFormatter.FormatPrice(
                Of(14_900, Currency.Eur), Locale.Parse("de-DE"),
                new TaxDisplayContext(TaxPresentation.TaxInclusive, displayUnitPricePerKilogram: true),
                Of(2_483, Currency.Usd)));
    }

    [Fact]
    [Requirement("CC-PRC-002")]
    public void An_unresolved_tax_convention_never_displays()
    {
        Assert.Throws<PricingValidationException>(
            () => new TaxDisplayContext(default, displayUnitPricePerKilogram: false));
    }
}
