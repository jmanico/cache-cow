using CacheCow.SharedKernel;

namespace CacheCow.Modules.PricingPromotions.Pricing;

/// <summary>
/// The fixed market-to-currency assignment of CC-PRC-001: US=USD, ES=EUR,
/// MX=MXN, DE=EUR, JP=JPY, IN=INR. This is requirement data, not policy the
/// module invents; every price and fixed-amount discount in this context is
/// validated against it, and no runtime FX conversion exists anywhere
/// (CC-PRC-001 negative case).
/// </summary>
public static class LaunchMarketCurrencies
{
    private static readonly Dictionary<Market, Currency> ByMarket =
        new()
        {
            [Market.US] = Currency.Usd,
            [Market.ES] = Currency.Eur,
            [Market.MX] = Currency.Mxn,
            [Market.DE] = Currency.Eur,
            [Market.JP] = Currency.Jpy,
            [Market.IN] = Currency.Inr,
        };

    /// <summary>The single consumer currency of a launch market (CC-PRC-001).</summary>
    public static Currency CurrencyOf(Market market) =>
        ByMarket.TryGetValue(market, out var currency)
            ? currency
            : throw new PricingValidationException(
                "Unknown or uninitialized market; only the six launch markets carry consumer prices (CC-PRC-001, CC-MKT-001).");
}
