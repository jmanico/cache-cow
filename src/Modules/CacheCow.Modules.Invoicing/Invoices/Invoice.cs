using CacheCow.Modules.Invoicing.Tax;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.Invoicing.Invoices;

/// <summary>
/// An issued invoice — an immutable legal financial record (CC-INV-001).
///
/// Immutability is structural: every property is get-only and assigned once in
/// the internal constructor; collections are defensive read-only copies; the
/// type exposes no setter, no mutator, and no "edit" operation of any kind
/// (issue 046, AC-06). Corrections happen exclusively through
/// <see cref="CreditNote"/> records that reference this invoice and never touch
/// it (ARCHITECTURE.md, Dependency rule 6). The application-layer guarantee is
/// backed at the database-privilege level by INSERT-only roles and WORM
/// retention (CC-SEC-020; SECURITY.md, Logging rule 6) — that adapter lands
/// with the persistence issues (015/081) and is not weakened here.
///
/// Issued invoices are also the documented legal-hold erasure exception: a
/// data-subject erasure request never mutates them (CC-CMP-003; issue 090).
/// Only <see cref="InvoiceIssuer"/> creates instances, so a number is always
/// allocated from the per-legal-entity sequence (CC-INV-001).
/// </summary>
public sealed class Invoice
{
    internal Invoice(
        InvoiceId id,
        InvoiceNumber number,
        InvoiceDraft draft,
        Money subtotal,
        Money total)
    {
        Id = id;
        Number = number;
        LegalEntity = draft.LegalEntity;
        Market = draft.Market;
        Order = draft.Order;
        CustomerAccount = draft.CustomerAccount;
        Lines = [.. draft.Lines];
        TaxContent = draft.TaxContent;
        IssuedAt = draft.IssuedAt;
        Subtotal = subtotal;
        Total = total;
    }

    /// <summary>Opaque, unguessable technical identity (never the sequential number — CC-ORD-010).</summary>
    public InvoiceId Id { get; }

    /// <summary>Sequential legal number per legal entity (CC-INV-001). Presentation only; never an access path.</summary>
    public InvoiceNumber Number { get; }

    public Numbering.LegalEntityId LegalEntity { get; }

    public Market Market { get; }

    public OrderReference Order { get; }

    /// <summary>Null for guest orders; guest access uses the CC-ORD-010 capability token only.</summary>
    public AccountReference? CustomerAccount { get; }

    public IReadOnlyList<InvoiceLine> Lines { get; }

    /// <summary>Typed per-market tax content (issue 047); structured fields, never free text.</summary>
    public MarketTaxContent TaxContent { get; }

    public DateTimeOffset IssuedAt { get; }

    /// <summary>Sum of line totals, overflow-checked (CC-PRC-003).</summary>
    public Money Subtotal { get; }

    /// <summary>
    /// Grand total per the market's tax convention (CC-PRC-002): tax-inclusive
    /// markets already carry tax inside line prices; the US adds sales tax on
    /// top of the tax-exclusive subtotal.
    /// </summary>
    public Money Total { get; }
}
