using CacheCow.SharedKernel;

namespace CacheCow.Modules.PricingPromotions.Promotions;

/// <summary>
/// Configuration-backed <see cref="IMarketTimeZoneProvider"/>: an explicit
/// market-to-IANA-zone mapping injected by the host. Deliberately ships no
/// default entries — the per-market canonical timezone is an open decision
/// (issue 033, Open Questions) and must not be guessed here.
/// </summary>
public sealed class MarketTimeZoneMap : IMarketTimeZoneProvider
{
    private readonly Dictionary<Market, TimeZoneInfo> _zones;

    public MarketTimeZoneMap(IReadOnlyDictionary<Market, string> ianaZoneIdByMarket)
    {
        ArgumentNullException.ThrowIfNull(ianaZoneIdByMarket);

        _zones = new Dictionary<Market, TimeZoneInfo>();
        foreach (var (market, zoneId) in ianaZoneIdByMarket)
        {
            // Validates the market is a launch market (CC-MKT-001).
            _ = Pricing.LaunchMarketCurrencies.CurrencyOf(market);

            try
            {
                _zones[market] = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
            }
            catch (TimeZoneNotFoundException exception)
            {
                throw new PricingValidationException(
                    $"'{zoneId}' is not a known timezone identifier for market {market.Code} (CC-PRC-006).", exception);
            }
            catch (InvalidTimeZoneException exception)
            {
                throw new PricingValidationException(
                    $"'{zoneId}' resolved to invalid timezone data for market {market.Code} (CC-PRC-006).", exception);
            }
        }
    }

    public bool TryGetTimeZone(Market market, out TimeZoneInfo? timeZone)
    {
        if (_zones.TryGetValue(market, out var found))
        {
            timeZone = found;
            return true;
        }

        timeZone = null;
        return false;
    }
}
