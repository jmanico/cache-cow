using CacheCow.SharedKernel;

namespace CacheCow.Modules.PricingPromotions.Promotions;

/// <summary>
/// Fail-closed placeholder <see cref="IMarketTimeZoneProvider"/>: maps no
/// market, so any promotion-window evaluation throws
/// <see cref="MarketTimeZoneUnavailableException"/> and never grants a
/// discount. Registered provisionally (TryAdd) because the canonical
/// market-to-IANA-timezone mapping is an open decision a human must ratify
/// (issue 033, Open Questions); the host replaces this with a configured
/// <see cref="MarketTimeZoneMap"/> once decided — the same provisional-default
/// pattern the MarketGating module uses for its undecided ports.
/// </summary>
public sealed class UnconfiguredMarketTimeZoneProvider : IMarketTimeZoneProvider
{
    public bool TryGetTimeZone(Market market, out TimeZoneInfo? timeZone)
    {
        timeZone = null;
        return false;
    }
}
