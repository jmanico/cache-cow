namespace CacheCow.Modules.PricingPromotions.Formatting;

/// <summary>
/// Currency-code disambiguation per DESIGN.md §4.4: es-MX shows
/// "$149.00 MXN" "where cross-currency ambiguity exists". The trigger condition
/// is undefined in the specs (issue 034, Open Questions), so it is exposed as a
/// caller option rather than decided here. FLAGGED for ratification.
/// </summary>
public enum CurrencyDisambiguation
{
    /// <summary>Locale-formatted amount only.</summary>
    None = 0,

    /// <summary>Append the ISO 4217 code after the locale-formatted amount (e.g. "$149.00 MXN").</summary>
    AppendIsoCurrencyCode = 1,
}

/// <summary>Presentation options for price formatting (issue 034).</summary>
public sealed record PriceFormatOptions(CurrencyDisambiguation Disambiguation = CurrencyDisambiguation.None)
{
    public static PriceFormatOptions Default { get; } = new();
}
