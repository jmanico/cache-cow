using CacheCow.Modules.Invoicing.Invoices;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.Invoicing.Tax;

/// <summary>
/// The fixed market→currency assignment of CC-PRC-001 (US=USD, ES=EUR, MX=MXN,
/// DE=EUR, JP=JPY, IN=INR), used to validate that every monetary amount on an
/// invoice is denominated in the invoice market's currency — no runtime FX
/// conversion exists anywhere (CC-PRC-001).
/// </summary>
public static class LaunchMarketCurrencies
{
    public static Currency For(Market market)
    {
        if (market == Market.US)
        {
            return Currency.Usd;
        }

        if (market == Market.ES || market == Market.DE)
        {
            return Currency.Eur;
        }

        if (market == Market.MX)
        {
            return Currency.Mxn;
        }

        if (market == Market.JP)
        {
            return Currency.Jpy;
        }

        if (market == Market.IN)
        {
            return Currency.Inr;
        }

        // Structurally unreachable for the closed launch-market set
        // (CC-MKT-001) — but money paths fail closed, never default
        // (SECURITY.md, Logging rule 2).
        throw new InvoiceValidationException(
            $"No launch currency is defined for market '{market.Code}' (CC-PRC-001; CC-MKT-001).");
    }
}
