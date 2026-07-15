using CacheCow.Modules.Invoicing.Invoices;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.Invoicing.Tax;

/// <summary>
/// JP invoice tax content: consumption tax with the qualified-invoice number
/// required by CC-INV-001; tax-inclusive per CC-PRC-002. All amounts are JPY —
/// zero-decimal minor units (CC-PRC-003; CC-QA-004). The qualified-invoice
/// registration number belongs to a legal entity, which is unenumerated
/// (issue 046, Open Questions).
/// </summary>
public sealed class JpConsumptionTaxContent : MarketTaxContent
{
    public JpConsumptionTaxContent(string qualifiedInvoiceNumber, IReadOnlyList<JpConsumptionTaxLine> taxLines)
        : base(Market.JP, amountsAreTaxInclusive: true)
    {
        ArgumentNullException.ThrowIfNull(taxLines);
        if (taxLines.Count == 0)
        {
            throw new InvoiceValidationException(
                "A JP invoice must carry at least one consumption tax line (CC-INV-001).");
        }

        QualifiedInvoiceNumber = RequireIdentifier(qualifiedInvoiceNumber, "The JP qualified-invoice number");
        TaxLines = [.. taxLines];
        TaxTotal = SumTax(taxLines.Select(line => line.Amount), Currency.Jpy);
    }

    /// <summary>JP qualified-invoice (tekikaku seikyūsho) registration number (CC-INV-001).</summary>
    public string QualifiedInvoiceNumber { get; }

    public IReadOnlyList<JpConsumptionTaxLine> TaxLines { get; }

    public override Money TaxTotal { get; }
}

/// <summary>A single JP consumption tax line: applied rate and tax amount (JPY, zero-decimal).</summary>
public sealed class JpConsumptionTaxLine
{
    public JpConsumptionTaxLine(decimal rate, Money amount)
    {
        Rate = rate is >= 0m and <= 1m
            ? rate
            : throw new InvoiceValidationException("A consumption tax rate must be a fraction in [0, 1] (CC-INV-001).");
        Amount = amount;
    }

    /// <summary>Exact decimal fraction (e.g. 0.10m); never binary floating point (CC-PRC-003).</summary>
    public decimal Rate { get; }

    public Money Amount { get; }
}
