using CacheCow.Modules.Invoicing.Invoices;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.Invoicing.Tax;

/// <summary>
/// Base of the closed set of per-market invoice tax content shapes required by
/// CC-INV-001: US sales tax lines; EU VAT (ES/DE) with rates and USt-IdNr.;
/// JP consumption tax with qualified-invoice number; IN GST with GSTIN and
/// per-line HSN codes. The constructor is <c>private protected</c>, so the
/// hierarchy cannot be extended outside this module: an invoice for market X
/// can only ever carry exactly market X's shape (issue 047), and tax content
/// is stored structured — typed fields, never free text (CC-INV-002 principle;
/// issue 047, Implementation Notes).
///
/// MX carries no ratified shape: see <see cref="MexicoTaxContent"/>, which
/// fails closed pending a human decision (issue 047, Open Questions).
/// </summary>
public abstract class MarketTaxContent
{
    private protected MarketTaxContent(Market market, bool amountsAreTaxInclusive)
    {
        Market = market;
        AmountsAreTaxInclusive = amountsAreTaxInclusive;
    }

    /// <summary>The single market whose legal shape this content satisfies.</summary>
    public Market Market { get; }

    /// <summary>
    /// Tax-inclusion display convention per CC-PRC-002: US tax-exclusive;
    /// DE/ES/MX/JP/IN tax-inclusive (IN inclusive with an explicit GST line on
    /// the invoice).
    /// </summary>
    public bool AmountsAreTaxInclusive { get; }

    /// <summary>Total tax carried by this content, in the market's currency.</summary>
    public abstract Money TaxTotal { get; }

    private protected static Money SumTax(IEnumerable<Money> amounts, Currency expectedCurrency)
    {
        ArgumentNullException.ThrowIfNull(amounts);
        var total = Money.FromMinorUnits(0, expectedCurrency);
        foreach (var amount in amounts)
        {
            if (!amount.Currency.Equals(expectedCurrency))
            {
                throw new InvoiceValidationException(
                    $"Tax amounts must be denominated in {expectedCurrency.Code} (CC-PRC-001).");
            }

            if (amount.MinorUnits < 0)
            {
                throw new InvoiceValidationException(
                    "Tax line amounts must not be negative; corrections are credit notes (CC-INV-001).");
            }

            total += amount; // overflow-checked (CC-PRC-003)
        }

        return total;
    }

    private protected static decimal RequireRate(decimal rate)
    {
        // Rates are exact decimals (never binary floating point — CC-PRC-003).
        if (rate < 0m || rate > 1m)
        {
            throw new InvoiceValidationException(
                "A tax rate must be a fraction in [0, 1] (e.g. 0.19m for 19 %).");
        }

        return rate;
    }

    private protected static string RequireIdentifier(string value, string description)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvoiceValidationException(
                $"{description} is required and must be non-empty (CC-INV-001); issuance fails closed without it (issue 047, Failure Behavior).");
        }

        return value;
    }
}
