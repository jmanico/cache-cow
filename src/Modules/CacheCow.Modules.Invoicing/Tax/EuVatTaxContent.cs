using CacheCow.Modules.Invoicing.Invoices;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.Invoicing.Tax;

/// <summary>
/// EU VAT invoice tax content for the ES and DE markets: VAT lines with the
/// applied rate(s) and the seller VAT identifier (USt-IdNr.) required by
/// CC-INV-001; tax-inclusive per CC-PRC-002. Amounts via Stripe Tax
/// (ARCHITECTURE.md, Payments).
///
/// Open (issue 047): CC-INV-001 names only the German USt-IdNr.; whether ES
/// invoices carry a Spanish VAT identifier instead is unresolved — this model
/// requires one seller VAT identifier per invoice and leaves the per-market
/// identifier scheme to that human decision. The identifier value belongs to a
/// legal entity, which is itself unenumerated (issue 046, Open Questions).
/// </summary>
public sealed class EuVatTaxContent : MarketTaxContent
{
    public EuVatTaxContent(Market market, string sellerVatIdentifier, IReadOnlyList<EuVatLine> vatLines)
        : base(RequireEuMarket(market), amountsAreTaxInclusive: true)
    {
        ArgumentNullException.ThrowIfNull(vatLines);
        if (vatLines.Count == 0)
        {
            throw new InvoiceValidationException(
                "An EU invoice must carry at least one VAT line with its rate (CC-INV-001).");
        }

        SellerVatIdentifier = RequireIdentifier(sellerVatIdentifier, "The seller VAT identifier (USt-IdNr.)");
        VatLines = [.. vatLines];
        TaxTotal = SumTax(vatLines.Select(line => line.Amount), Currency.Eur);
    }

    /// <summary>Seller VAT identifier (USt-IdNr. — CC-INV-001).</summary>
    public string SellerVatIdentifier { get; }

    public IReadOnlyList<EuVatLine> VatLines { get; }

    public override Money TaxTotal { get; }

    private static Market RequireEuMarket(Market market)
    {
        if (market != Market.ES && market != Market.DE)
        {
            throw new InvoiceValidationException(
                $"EU VAT content applies only to the ES and DE markets, not '{market.Code}' (CC-INV-001).");
        }

        return market;
    }
}

/// <summary>A single EU VAT line: applied rate and VAT amount (EUR).</summary>
public sealed class EuVatLine
{
    public EuVatLine(decimal rate, Money amount)
    {
        Rate = rate is >= 0m and <= 1m
            ? rate
            : throw new InvoiceValidationException("A VAT rate must be a fraction in [0, 1] (CC-INV-001).");
        Amount = amount;
    }

    /// <summary>Exact decimal fraction (e.g. 0.19m); never binary floating point (CC-PRC-003).</summary>
    public decimal Rate { get; }

    public Money Amount { get; }
}
