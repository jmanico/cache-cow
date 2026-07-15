using CacheCow.SharedKernel;

namespace CacheCow.Modules.MarketGating.Resolution;

/// <summary>
/// Null adapter: proposes no market. Registered as the default so composition
/// works before a geolocation provider is chosen (issue 024, Open Questions —
/// no provider is named in the canonical docs). A missing proposal degrades
/// only the default-market suggestion, never gating correctness (issue 024,
/// Failure Behavior).
/// </summary>
public sealed class NullGeolocationMarketProposer : IGeolocationMarketProposer
{
    public Market? ProposeMarket(string? clientIpAddress) => null;
}
