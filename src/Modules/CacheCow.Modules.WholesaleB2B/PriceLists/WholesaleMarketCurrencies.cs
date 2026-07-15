using CacheCow.SharedKernel;

namespace CacheCow.Modules.WholesaleB2B.PriceLists;

/// <summary>
/// The fixed market-to-currency assignment of CC-PRC-001 (US=USD, ES=EUR,
/// MX=MXN, DE=EUR, JP=JPY, IN=INR), restated locally because bounded contexts
/// share only the minimal kernel (ARCHITECTURE.md, Dependency rule 9). Every
/// wholesale price is validated against it (issue 050, AC-01); no runtime FX
/// conversion exists anywhere.
/// </summary>
internal static class WholesaleMarketCurrencies
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

    internal static Currency CurrencyOf(Market market) =>
        ByMarket.TryGetValue(market, out var currency)
            ? currency
            : throw new WholesaleValidationException(
                "Unknown or uninitialized market; only the six launch markets carry wholesale prices (CC-WHS-001, CC-PRC-001, CC-MKT-001).");
}
