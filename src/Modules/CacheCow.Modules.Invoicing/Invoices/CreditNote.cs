using CacheCow.Modules.Invoicing.Numbering;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.Invoicing.Invoices;

/// <summary>
/// The ONLY correction mechanism for an issued invoice (CC-INV-001;
/// ARCHITECTURE.md, Dependency rule 6: corrections are new records, never
/// mutations). A credit note is itself an immutable, sequentially numbered
/// record (its own per-legal-entity series, so it can never gap the invoice
/// series) that references the original invoice and never modifies it.
/// Created only by <see cref="InvoiceIssuer.IssueCreditNote"/>.
/// </summary>
public sealed class CreditNote
{
    internal CreditNote(
        InvoiceNumber number,
        LegalEntityId legalEntity,
        InvoiceId originalInvoiceId,
        InvoiceNumber originalInvoiceNumber,
        string reason,
        Money creditedAmount,
        DateTimeOffset issuedAt)
    {
        Number = number;
        LegalEntity = legalEntity;
        OriginalInvoiceId = originalInvoiceId;
        OriginalInvoiceNumber = originalInvoiceNumber;
        Reason = reason;
        CreditedAmount = creditedAmount;
        IssuedAt = issuedAt;
    }

    /// <summary>Sequential number in the legal entity's credit-note series (CC-INV-001).</summary>
    public InvoiceNumber Number { get; }

    public LegalEntityId LegalEntity { get; }

    /// <summary>Reference to the corrected invoice — the original stays byte-for-byte unchanged (issue 046, AC-03).</summary>
    public InvoiceId OriginalInvoiceId { get; }

    public InvoiceNumber OriginalInvoiceNumber { get; }

    public string Reason { get; }

    /// <summary>Positive amount credited back, in the original invoice's currency (CC-PRC-001/003).</summary>
    public Money CreditedAmount { get; }

    public DateTimeOffset IssuedAt { get; }
}
