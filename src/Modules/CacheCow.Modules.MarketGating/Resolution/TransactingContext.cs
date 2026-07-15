using CacheCow.SharedKernel;

namespace CacheCow.Modules.MarketGating.Resolution;

/// <summary>
/// The server-side transacting market + locale state — the ONLY input gating
/// decisions and cache keys accept (CC-SEC-012; SECURITY.md, Authentication
/// rule 10; CC-MKT-009). Valid by construction: it can only be built from a
/// launch market and a launch locale, so no client hint (Accept-Language,
/// geolocation header, forged cookie) can ever become transacting state
/// without passing the closed-set validation in
/// <see cref="TransactingContextResolver"/>.
/// </summary>
public sealed record TransactingContext
{
    public TransactingContext(Market market, Locale locale)
    {
        if (!Market.All.Contains(market))
        {
            throw new ArgumentException(
                "Transacting market must be one of the six launch markets (CC-MKT-001); reject, never coerce (SECURITY.md, Input validation rule 1).",
                nameof(market));
        }

        if (!LaunchLocales.Contains(locale))
        {
            throw new ArgumentException(
                "Transacting locale must be one of the seven launch locales (CC-I18N-001); reject, never coerce (SECURITY.md, Input validation rule 1).",
                nameof(locale));
        }

        Market = market;
        Locale = locale;
    }

    /// <summary>The transacting market: drives catalog, currency, tax, and compliance regime (REQUIREMENTS.md §2).</summary>
    public Market Market { get; }

    /// <summary>The UI locale: drives strings and formatting, independent of market (CC-I18N-001).</summary>
    public Locale Locale { get; }
}
