using CacheCow.SharedKernel;

namespace CacheCow.Modules.MarketGating.Resolution;

/// <summary>
/// Port for the IP-geolocation adapter. IP-derived geolocation is UNTRUSTED
/// personalization data: it may only propose a default market, which the user
/// can override; it never participates in a gating decision (CC-MKT-002,
/// CC-SEC-012; SECURITY.md, Authentication rule 10). No provider is named in
/// any canonical doc (issue 024, Open Questions), so no real adapter exists —
/// only <see cref="NullGeolocationMarketProposer"/>, which proposes nothing.
/// </summary>
public interface IGeolocationMarketProposer
{
    /// <summary>
    /// Proposes a default market from the client IP, or null when no proposal
    /// can be made. The return value is a proposal only; the resolver
    /// re-validates it against the launch set before use.
    /// </summary>
    Market? ProposeMarket(string? clientIpAddress);
}
