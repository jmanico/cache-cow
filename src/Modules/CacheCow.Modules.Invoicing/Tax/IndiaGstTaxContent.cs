using CacheCow.Modules.Invoicing.Invoices;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.Invoicing.Tax;

/// <summary>
/// IN invoice tax content: GST with GSTIN and per-line HSN codes per
/// CC-INV-001, tax-inclusive with the GST line explicit on the invoice per
/// CC-PRC-002. Computed via Razorpay/local accounting rules (ARCHITECTURE.md,
/// Payments). All amounts INR.
///
/// Open (issue 047): the HSN-code source is unspecified (CC-CAT-001 has no HSN
/// field), and whether the seller's GSTIN, the buyer's (CC-WHS-002), or both
/// must appear is not stated — this model carries the seller GSTIN and leaves
/// buyer-GSTIN modeling to that human decision.
/// </summary>
public sealed class IndiaGstTaxContent : MarketTaxContent
{
    public IndiaGstTaxContent(string sellerGstin, IReadOnlyList<IndiaGstLineDetail> lineDetails)
        : base(Market.IN, amountsAreTaxInclusive: true)
    {
        ArgumentNullException.ThrowIfNull(lineDetails);
        if (lineDetails.Count == 0)
        {
            throw new InvoiceValidationException(
                "An IN invoice must carry GST detail with an HSN code for every line (CC-INV-001).");
        }

        SellerGstin = RequireIdentifier(sellerGstin, "The seller GSTIN");
        LineDetails = [.. lineDetails];
        TaxTotal = SumTax(lineDetails.Select(detail => detail.GstAmount), Currency.Inr);
    }

    /// <summary>Seller GST identification number (CC-INV-001).</summary>
    public string SellerGstin { get; }

    /// <summary>Per-invoice-line GST detail; must cover every invoice line exactly (CC-INV-001).</summary>
    public IReadOnlyList<IndiaGstLineDetail> LineDetails { get; }

    public override Money TaxTotal { get; }
}

/// <summary>
/// GST detail for one invoice line: 1-based line number, the line's HSN code
/// (required per line by CC-INV-001), applied rate, and GST amount (INR).
/// </summary>
public sealed class IndiaGstLineDetail
{
    public IndiaGstLineDetail(int lineNumber, string hsnCode, decimal rate, Money gstAmount)
    {
        if (lineNumber < 1)
        {
            throw new InvoiceValidationException("GST line detail numbers are 1-based (CC-INV-001).");
        }

        if (string.IsNullOrWhiteSpace(hsnCode))
        {
            throw new InvoiceValidationException(
                "Every IN invoice line requires an HSN code (CC-INV-001); issuance fails closed without it.");
        }

        LineNumber = lineNumber;
        HsnCode = hsnCode;
        Rate = rate is >= 0m and <= 1m
            ? rate
            : throw new InvoiceValidationException("A GST rate must be a fraction in [0, 1] (CC-INV-001).");
        GstAmount = gstAmount;
    }

    public int LineNumber { get; }

    public string HsnCode { get; }

    /// <summary>Exact decimal fraction; never binary floating point (CC-PRC-003).</summary>
    public decimal Rate { get; }

    public Money GstAmount { get; }
}
