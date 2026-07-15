using System.Globalization;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.PricingPromotions.Formatting;

/// <summary>
/// Server-side locale-aware price formatting (CC-PRC-004): every string comes
/// from .NET/ICU globalization data — the server equivalent of
/// <c>Intl.NumberFormat</c> — via <see cref="CultureInfo"/> currency
/// formatting. Hand-formatted currency strings (concatenated symbols,
/// hardcoded separators, custom grouping) are a defect and do not exist here:
/// grouping (including INR lakh/crore), separators, and symbol placement all
/// derive from the locale; symbol and decimal digits derive from the
/// <see cref="Money"/> value's currency (JPY zero-decimal). The locale changes
/// formatting only — amount and currency always come from the server-side
/// Money value, never from the locale or any client hint (CC-SEC-012).
/// Formatting never computes money (CC-PRC-005): the €/kg value is derived by
/// the price model (issue 032) and passed in.
/// </summary>
public static class PriceFormatter
{
    /// <summary>
    /// The ICU culture each launch currency's symbol is sourced from, so that a
    /// price keeps its own currency's symbol even when rendered in another
    /// locale (CC-SEC-012: locale never changes the currency). Symbols come
    /// from CLDR data, never from hardcoded strings (CC-PRC-004).
    /// </summary>
    private static readonly Dictionary<string, string> SymbolSourceCultureByCurrencyCode =
        new(StringComparer.Ordinal)
        {
            ["USD"] = "en-US",
            ["EUR"] = "de-DE",
            ["MXN"] = "es-MX",
            ["JPY"] = "ja-JP",
            ["INR"] = "en-IN",
        };

    /// <summary>Formats a monetary amount for a locale (CC-PRC-004; DESIGN.md §4.4 worked examples).</summary>
    public static string FormatAmount(Money amount, Locale locale, PriceFormatOptions? options = null)
    {
        var culture = ResolveCulture(locale);

        var format = (NumberFormatInfo)culture.NumberFormat.Clone();
        format.CurrencySymbol = SymbolFor(amount.Currency);
        format.CurrencyDecimalDigits = amount.Currency.MinorUnitExponent;

        var formatted = amount.ToDecimal().ToString("C", format);

        if ((options ?? PriceFormatOptions.Default).Disambiguation == CurrencyDisambiguation.AppendIsoCurrencyCode)
        {
            // DESIGN.md §4.4 disambiguation suffix ("$149.00 MXN"): the amount
            // itself stays locale-formatted; only the ISO 4217 code is appended.
            // The trigger condition is an open question (issue 034) — callers opt in.
            formatted = string.Create(CultureInfo.InvariantCulture, $"{formatted} {amount.Currency.Code}");
        }

        return formatted;
    }

    /// <summary>
    /// Formats a price with its market display-convention data (CC-PRC-002):
    /// structured result of formatted amount, formatted €/kg when the
    /// convention requires one, and the applicable note keys. When the
    /// convention requires a unit price per kilogram and none was supplied, the
    /// price fails closed rather than rendering without its legally required
    /// companion (issue 034, Failure Behavior).
    /// </summary>
    public static FormattedPrice FormatPrice(
        Money price,
        Locale locale,
        TaxDisplayContext taxDisplay,
        Money? unitPricePerKilogram = null,
        PriceFormatOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(taxDisplay);

        var notes = new List<PriceDisplayNote>
        {
            taxDisplay.Presentation switch
            {
                TaxPresentation.TaxInclusive => PriceDisplayNote.TaxInclusive,
                TaxPresentation.TaxExclusiveEstimatedAtCheckout => PriceDisplayNote.TaxExclusiveEstimatedTaxAtCheckout,
                _ => throw new PricingValidationException(
                    "Unresolved tax presentation; refusing to display (CC-PRC-002; fail closed)."),
            },
        };

        string? formattedUnitPrice = null;
        if (taxDisplay.DisplayUnitPricePerKilogram)
        {
            if (unitPricePerKilogram is not { } unitPrice)
            {
                throw new PricingValidationException(
                    "This market's convention requires a unit price per kilogram alongside every price (Preisangabenverordnung, CC-PRC-002); none was supplied, so the price is not displayed (fail closed).");
            }

            if (!unitPrice.Currency.Equals(price.Currency))
            {
                throw new PricingValidationException(
                    $"Unit price per kilogram is denominated in {unitPrice.Currency.Code} but the price is {price.Currency.Code} (CC-PRC-001).");
            }

            formattedUnitPrice = FormatAmount(unitPrice, locale, options);
            notes.Add(PriceDisplayNote.UnitPricePerKilogram);
        }

        return new FormattedPrice(FormatAmount(price, locale, options), formattedUnitPrice, notes);
    }

    private static CultureInfo ResolveCulture(Locale locale)
    {
        try
        {
            // predefinedOnly: a well-formed BCP 47 tag with no ICU/CLDR data is
            // rejected, never synthesized into a made-up format (issue 034,
            // Failure Behavior: unknown locale is rejected at the boundary).
            return CultureInfo.GetCultureInfo(locale.Tag, predefinedOnly: true);
        }
        catch (CultureNotFoundException exception)
        {
            throw new PricingValidationException(
                $"Locale '{locale.Tag}' has no culture data; refusing to hand-format (CC-PRC-004; fail closed).", exception);
        }
    }

    private static string SymbolFor(Currency currency) =>
        CultureInfo.GetCultureInfo(SymbolSourceCultureByCurrencyCode[currency.Code])
            .NumberFormat.CurrencySymbol;
}
