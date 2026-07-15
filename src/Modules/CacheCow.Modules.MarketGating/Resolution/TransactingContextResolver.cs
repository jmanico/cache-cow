using CacheCow.SharedKernel;

namespace CacheCow.Modules.MarketGating.Resolution;

/// <summary>
/// Resolves the server-side transacting market/locale state (CC-MKT-002,
/// CC-I18N-001, CC-SEC-012):
/// precedence is persisted explicit choice &gt; geolocation proposal; the
/// explicit choice persists across sessions and geolocation never silently
/// overrides it. Every client-supplied identifier is validated against the
/// closed launch sets (six markets, seven locales) and rejected otherwise —
/// Accept-Language and other client hints are not inputs to this type at all,
/// by construction (SECURITY.md, Authentication rule 10; Input validation
/// rule 1). Market and locale are independent selections; neither is ever
/// inferred from the other (DESIGN.md §7).
/// </summary>
public sealed class TransactingContextResolver
{
    private readonly IMarketPreferenceStore _preferences;
    private readonly IGeolocationMarketProposer _geolocation;

    public TransactingContextResolver(
        IMarketPreferenceStore preferences,
        IGeolocationMarketProposer geolocation)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(geolocation);
        _preferences = preferences;
        _geolocation = geolocation;
    }

    /// <summary>
    /// Validates and persists an explicit market selection (CC-MKT-002). Any
    /// code outside the six launch markets is rejected and never becomes
    /// transacting state (issue 024 AC-05); the caller logs the rejection as a
    /// validation event (SECURITY.md, Logging rule 3). Locale is untouched —
    /// independent selections (CC-I18N-001).
    /// </summary>
    public bool TrySelectMarket(PreferenceSubject subject, string? marketCode, out Market market)
    {
        ArgumentNullException.ThrowIfNull(subject);

        if (!Market.TryParse(marketCode, out market))
        {
            return false;
        }

        var existing = _preferences.Find(subject);
        _preferences.Save(subject, new MarketLocalePreference(market, existing?.Locale));
        return true;
    }

    /// <summary>
    /// Validates and persists an explicit locale selection against the closed
    /// launch-locale set (CC-I18N-001; issue 024 AC-05). Market is untouched —
    /// never inferred from locale (DESIGN.md §7).
    /// </summary>
    public bool TrySelectLocale(PreferenceSubject subject, string? localeTag, out Locale locale)
    {
        ArgumentNullException.ThrowIfNull(subject);

        if (!LaunchLocales.TryParse(localeTag, out locale))
        {
            return false;
        }

        var existing = _preferences.Find(subject);
        _preferences.Save(subject, new MarketLocalePreference(existing?.Market, locale));
        return true;
    }

    /// <summary>
    /// Per-request resolution: the persisted explicit choice wins; only when
    /// no explicit market choice exists does IP geolocation contribute — and
    /// then only as an overridable proposal (CC-MKT-002; SECURITY.md,
    /// Authentication rule 10). A failed or foreign geolocation degrades the
    /// proposal only; there is no guessed default market — the fallback market
    /// is an open question in issue 024 and is not resolved here.
    /// </summary>
    public MarketResolution Resolve(PreferenceSubject subject, string? clientIpAddress)
    {
        ArgumentNullException.ThrowIfNull(subject);

        var preference = _preferences.Find(subject);

        // Persisted values are typed, but re-validate against the closed sets
        // anyway: a stored value is still input to a gating path and is never
        // trusted raw (issue 024 AC-07; fail closed per SECURITY.md, Logging rule 2).
        var explicitMarket = preference?.Market is { } storedMarket && Market.All.Contains(storedMarket)
            ? storedMarket
            : (Market?)null;
        var locale = preference?.Locale is { } storedLocale && LaunchLocales.Contains(storedLocale)
            ? storedLocale
            : (Locale?)null;

        if (explicitMarket is { } chosen)
        {
            return MarketResolution.FromExplicitChoice(chosen, locale);
        }

        var proposal = ProposeFromGeolocation(clientIpAddress);
        return proposal is { } proposed
            ? MarketResolution.FromGeolocationProposal(proposed, locale)
            : MarketResolution.Unresolved(locale);
    }

    private Market? ProposeFromGeolocation(string? clientIpAddress)
    {
        // The adapter is an external, untrusted input source: its output is
        // re-validated against the launch set, and a value it cannot vouch for
        // (including an uninitialized Market) yields no proposal. A lookup
        // failure degrades the proposal only — never gating correctness, and
        // never a guessed market (issue 024, Failure Behavior).
        Market? proposal;
        try
        {
            proposal = _geolocation.ProposeMarket(clientIpAddress);
        }
#pragma warning disable CA1031 // Geolocation is an untrusted external adapter; its failure yields "no proposal", never a broader resolution (SECURITY.md, Logging rule 2).
        catch (Exception)
#pragma warning restore CA1031
        {
            return null;
        }

        return proposal is { } market && Market.All.Contains(market) ? market : null;
    }
}
