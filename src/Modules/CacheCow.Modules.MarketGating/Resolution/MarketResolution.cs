using CacheCow.SharedKernel;

namespace CacheCow.Modules.MarketGating.Resolution;

/// <summary>
/// The outcome of per-request transacting-state resolution (CC-MKT-002).
/// <see cref="Context"/> is non-null only when both market and locale resolved
/// from trusted server-side state; an unresolved result fails closed
/// downstream — gating denies and caching is no-store (issue 028 AC-05),
/// never a guessed default.
/// </summary>
public sealed record MarketResolution
{
    private MarketResolution(Market? market, Locale? locale, bool marketWasProposedFromGeolocation)
    {
        Market = market;
        Locale = locale;
        MarketWasProposedFromGeolocation = marketWasProposedFromGeolocation;
    }

    /// <summary>The resolved transacting market, or null when unresolved.</summary>
    public Market? Market { get; }

    /// <summary>The resolved UI locale, or null when the user has made no valid locale choice.</summary>
    public Locale? Locale { get; }

    /// <summary>
    /// True when the market came from IP geolocation: it is an overridable
    /// proposal, not an explicit user choice (CC-MKT-002; SECURITY.md,
    /// Authentication rule 10). The user can always select any launch market.
    /// </summary>
    public bool MarketWasProposedFromGeolocation { get; }

    /// <summary>
    /// The server-side transacting context when fully resolved; the sole input
    /// for gating decisions and cache keys (CC-SEC-012, CC-MKT-009).
    /// </summary>
    public TransactingContext? Context =>
        Market is { } market && Locale is { } locale ? new TransactingContext(market, locale) : null;

    public static MarketResolution FromExplicitChoice(Market? market, Locale? locale) =>
        new(market, locale, marketWasProposedFromGeolocation: false);

    public static MarketResolution FromGeolocationProposal(Market proposedMarket, Locale? locale) =>
        new(proposedMarket, locale, marketWasProposedFromGeolocation: true);

    public static MarketResolution Unresolved(Locale? locale) =>
        new(null, locale, marketWasProposedFromGeolocation: false);
}
