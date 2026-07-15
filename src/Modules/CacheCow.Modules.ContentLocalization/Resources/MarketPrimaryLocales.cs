using CacheCow.SharedKernel;

namespace CacheCow.Modules.ContentLocalization.Resources;

/// <summary>
/// Maps each market to its primary language for template fallback
/// (CC-I18N-006: "fallback is the market's primary language, never a broken
/// template"). The five unambiguous mappings are derived from CC-I18N-001's
/// launch-locale list (US→en-US, ES→es-ES, MX→es-MX, DE→de-DE, JP→ja-JP).
/// IN carries two launch locales (en-IN and hi-IN) and the specs do not name a
/// primary — that is an open decision a human must make (CLAUDE.md working
/// rules), so IN has NO default here: it must be supplied explicitly via
/// <see cref="WithIndiaPrimary"/>, and an unconfigured IN fallback fails
/// closed with <see cref="MarketPrimaryLocaleUndecidedException"/>.
/// </summary>
public sealed class MarketPrimaryLocales
{
    private readonly IReadOnlyDictionary<Market, Locale> _primaries;

    private MarketPrimaryLocales(IReadOnlyDictionary<Market, Locale> primaries)
    {
        _primaries = primaries;
    }

    private static readonly IReadOnlyDictionary<Market, Locale> Unambiguous =
        new Dictionary<Market, Locale>
        {
            [Market.US] = Locale.Parse("en-US"),
            [Market.ES] = Locale.Parse("es-ES"),
            [Market.MX] = Locale.Parse("es-MX"),
            [Market.DE] = Locale.Parse("de-DE"),
            [Market.JP] = Locale.Parse("ja-JP"),
        };

    /// <summary>The five unambiguous mappings only; IN is deliberately absent.</summary>
    public static MarketPrimaryLocales Default { get; } = new(Unambiguous);

    /// <summary>
    /// Returns a map that additionally resolves IN to the supplied primary,
    /// which MUST be one of IN's launch locales (en-IN or hi-IN, CC-I18N-001).
    /// </summary>
    public static MarketPrimaryLocales WithIndiaPrimary(Locale indiaPrimary)
    {
        if (indiaPrimary != Locale.Parse("en-IN") && indiaPrimary != Locale.Parse("hi-IN"))
        {
            throw new ArgumentException(
                $"'{indiaPrimary}' is not an IN launch locale; the IN primary must be en-IN or hi-IN (CC-I18N-001).",
                nameof(indiaPrimary));
        }

        var primaries = new Dictionary<Market, Locale>(Unambiguous.ToDictionary())
        {
            [Market.IN] = indiaPrimary,
        };
        return new MarketPrimaryLocales(primaries);
    }

    public bool TryGetPrimaryLocale(Market market, out Locale locale) =>
        _primaries.TryGetValue(market, out locale);

    /// <summary>
    /// The market's primary locale for template fallback. Fails closed for a
    /// market whose primary language is an open decision (IN, unconfigured).
    /// </summary>
    public Locale GetPrimaryLocale(Market market) =>
        _primaries.TryGetValue(market, out var locale)
            ? locale
            : throw new MarketPrimaryLocaleUndecidedException(market);
}

/// <summary>
/// The market's primary language is an open decision (ARCHITECTURE.md, "Known
/// unknowns" discipline): fallback fails closed rather than guessing a locale
/// (CC-I18N-006 forbids a broken template; picking a language for IN would
/// resolve an open decision in code, which CLAUDE.md forbids).
/// </summary>
public sealed class MarketPrimaryLocaleUndecidedException : InvalidOperationException
{
    public MarketPrimaryLocaleUndecidedException(Market market)
        : base($"No primary locale is configured for market {market.Code}; the choice (e.g. en-IN vs hi-IN) is an open decision requiring explicit configuration (CC-I18N-006).")
    {
    }
}
