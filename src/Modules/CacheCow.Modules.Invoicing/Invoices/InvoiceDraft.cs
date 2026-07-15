using CacheCow.Modules.Invoicing.Numbering;
using CacheCow.Modules.Invoicing.Tax;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.Invoicing.Invoices;

/// <summary>
/// Everything required to issue an invoice, composed server-side from
/// canonical order/pricing data by the Ordering &amp; Payments context
/// (ARCHITECTURE.md, Dependency rule 2) — never from client-supplied values
/// (CC-PRC-005). A draft is not an invoice: it has no number and no legal
/// existence until <see cref="InvoiceIssuer.Issue"/> validates it and
/// allocates a number (CC-INV-001).
/// </summary>
public sealed class InvoiceDraft
{
    public InvoiceDraft(
        LegalEntityId legalEntity,
        Market market,
        OrderReference order,
        AccountReference? customerAccount,
        IReadOnlyList<InvoiceLine> lines,
        MarketTaxContent taxContent,
        DateTimeOffset issuedAt)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(taxContent);

        LegalEntity = legalEntity;
        Market = market;
        Order = order;
        CustomerAccount = customerAccount;
        Lines = [.. lines];
        TaxContent = taxContent;
        IssuedAt = issuedAt;
    }

    /// <summary>Required input — legal entities are unenumerated in the specs and never defaulted (issue 046, Open Questions).</summary>
    public LegalEntityId LegalEntity { get; }

    public Market Market { get; }

    public OrderReference Order { get; }

    /// <summary>Null for guest orders (CC-ORD-001): access then flows only through the CC-ORD-010 capability token.</summary>
    public AccountReference? CustomerAccount { get; }

    public IReadOnlyList<InvoiceLine> Lines { get; }

    public MarketTaxContent TaxContent { get; }

    public DateTimeOffset IssuedAt { get; }
}
