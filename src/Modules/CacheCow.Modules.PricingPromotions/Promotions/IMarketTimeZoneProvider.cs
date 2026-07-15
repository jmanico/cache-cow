using CacheCow.SharedKernel;

namespace CacheCow.Modules.PricingPromotions.Promotions;

/// <summary>
/// Supplies the timezone in which a market's promotion windows are interpreted
/// (CC-PRC-006 "start/end timestamps (market timezone)"). The specs do not
/// define the canonical zone per market — the US spans several — so the mapping
/// is required configuration (issue 033, Open Questions; awaiting a human
/// decision). No default mapping exists anywhere in this module.
/// </summary>
public interface IMarketTimeZoneProvider
{
    /// <summary>Resolves the market's configured timezone; false when the mapping has no entry.</summary>
    bool TryGetTimeZone(Market market, out TimeZoneInfo? timeZone);
}
