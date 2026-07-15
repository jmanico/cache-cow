using CacheCow.Modules.Invoicing.Invoices;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.Invoicing.Tax;

/// <summary>
/// US invoice tax content: sales tax line(s) per CC-INV-001, tax-exclusive
/// per CC-PRC-002. Amounts originate from Stripe Tax through the server-side
/// integration (ARCHITECTURE.md, Payments; issue 039) — never from the client
/// (CC-PRC-005).
/// </summary>
public sealed class UsSalesTaxContent : MarketTaxContent
{
    public UsSalesTaxContent(IReadOnlyList<UsSalesTaxLine> taxLines)
        : base(Market.US, amountsAreTaxInclusive: false)
    {
        ArgumentNullException.ThrowIfNull(taxLines);
        if (taxLines.Count == 0)
        {
            throw new InvoiceValidationException(
                "A US invoice must carry at least one sales tax line (CC-INV-001).");
        }

        TaxLines = [.. taxLines];
        TaxTotal = SumTax(taxLines.Select(line => line.Amount), Currency.Usd);
    }

    public IReadOnlyList<UsSalesTaxLine> TaxLines { get; }

    public override Money TaxTotal { get; }
}

/// <summary>A single US sales tax line: taxing jurisdiction, rate, amount (USD).</summary>
public sealed class UsSalesTaxLine
{
    public UsSalesTaxLine(string jurisdiction, decimal rate, Money amount)
    {
        if (string.IsNullOrWhiteSpace(jurisdiction))
        {
            throw new InvoiceValidationException("A US sales tax line requires a jurisdiction (CC-INV-001).");
        }

        Jurisdiction = jurisdiction;
        Rate = rate is >= 0m and <= 1m
            ? rate
            : throw new InvoiceValidationException("A tax rate must be a fraction in [0, 1].");
        Amount = amount;
    }

    public string Jurisdiction { get; }

    /// <summary>Exact decimal fraction (e.g. 0.0875m); never binary floating point (CC-PRC-003).</summary>
    public decimal Rate { get; }

    public Money Amount { get; }
}
