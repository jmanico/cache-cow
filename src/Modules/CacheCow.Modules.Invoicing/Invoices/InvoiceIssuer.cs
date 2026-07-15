using CacheCow.Modules.Invoicing.Numbering;
using CacheCow.Modules.Invoicing.Tax;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.Invoicing.Invoices;

/// <summary>
/// The single creation path for invoices and credit notes (CC-INV-001).
/// Validation runs to completion BEFORE a number is allocated, so a rejected
/// draft never consumes a sequence value — issuance fails closed and the
/// per-legal-entity series stays gapless (issue 046, Failure Behavior).
/// There is deliberately no update, edit, or delete counterpart: issued
/// documents are append-only sinks (ARCHITECTURE.md, Dependency rule 6).
/// </summary>
public sealed class InvoiceIssuer
{
    private readonly ILegalEntitySequence _sequence;

    public InvoiceIssuer(ILegalEntitySequence sequence)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        _sequence = sequence;
    }

    public Invoice Issue(InvoiceDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        // Struct references fail closed if uninitialized.
        _ = draft.LegalEntity.Value;
        _ = draft.Order.Value;

        if (draft.IssuedAt == default)
        {
            throw new InvoiceValidationException("An invoice requires its issuance timestamp.");
        }

        if (draft.Market == Market.MX)
        {
            // Epic open question 8: MX invoice tax content is not enumerated by
            // CC-INV-001 (issue 047, Open Questions). Fail closed pending a
            // human decision; never invent a legal format (CLAUDE.md).
            throw new UnratifiedMarketTaxContentException(
                "MX invoice issuance fails closed: CC-INV-001 enumerates no MX tax content "
                + "(open decision — issue 047, Open Questions).");
        }

        if (draft.Lines.Count == 0)
        {
            throw new InvoiceValidationException("An invoice requires at least one line (CC-INV-001).");
        }

        if (draft.TaxContent.Market != draft.Market)
        {
            throw new InvoiceValidationException(
                $"An invoice for market '{draft.Market.Code}' must carry exactly that market's tax content "
                + $"shape; got content for '{draft.TaxContent.Market.Code}' (CC-INV-001; issue 047).");
        }

        var currency = LaunchMarketCurrencies.For(draft.Market);
        var subtotal = Money.FromMinorUnits(0, currency);
        foreach (var line in draft.Lines)
        {
            if (!line.UnitPrice.Currency.Equals(currency))
            {
                throw new InvoiceValidationException(
                    $"Line '{line.LegalDescription}' is denominated in {line.UnitPrice.Currency.Code}; "
                    + $"the {draft.Market.Code} market invoices in {currency.Code} (CC-PRC-001).");
            }

            subtotal += line.LineTotal; // overflow-checked (CC-PRC-003)
        }

        RequireIndiaLineCoverage(draft);

        // CC-PRC-002: tax-inclusive markets carry tax inside prices; the US
        // adds sales tax on top of the tax-exclusive subtotal.
        var total = draft.TaxContent.AmountsAreTaxInclusive
            ? subtotal
            : subtotal + draft.TaxContent.TaxTotal;

        // Allocation is the LAST step: nothing after it can fail, so no number
        // is ever discarded (gapless per entity — CC-INV-001).
        var value = _sequence.AllocateNext(draft.LegalEntity, DocumentSeries.Invoice);
        var number = new InvoiceNumber(draft.LegalEntity, DocumentSeries.Invoice, value);

        return new Invoice(InvoiceId.NewId(), number, draft, subtotal, total);
    }

    /// <summary>
    /// Issues a credit note referencing <paramref name="original"/>. The
    /// original invoice is not — and cannot be — modified (issue 046, AC-03).
    /// </summary>
    public CreditNote IssueCreditNote(Invoice original, string reason, Money creditedAmount, DateTimeOffset issuedAt)
    {
        ArgumentNullException.ThrowIfNull(original);

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvoiceValidationException("A credit note requires a correction reason (CC-INV-001).");
        }

        if (issuedAt == default)
        {
            throw new InvoiceValidationException("A credit note requires its issuance timestamp.");
        }

        if (!creditedAmount.Currency.Equals(original.Total.Currency))
        {
            throw new InvoiceValidationException(
                "A credit note is denominated in the original invoice's currency (CC-PRC-001).");
        }

        if (creditedAmount.MinorUnits <= 0)
        {
            throw new InvoiceValidationException("A credit note credits a positive amount.");
        }

        if (creditedAmount > original.Total)
        {
            throw new InvoiceValidationException(
                "A credit note cannot credit more than the original invoice total.");
        }

        var value = _sequence.AllocateNext(original.LegalEntity, DocumentSeries.CreditNote);
        var number = new InvoiceNumber(original.LegalEntity, DocumentSeries.CreditNote, value);

        return new CreditNote(
            number,
            original.LegalEntity,
            original.Id,
            original.Number,
            reason,
            creditedAmount,
            issuedAt);
    }

    private static void RequireIndiaLineCoverage(InvoiceDraft draft)
    {
        if (draft.TaxContent is not IndiaGstTaxContent gst)
        {
            return;
        }

        // CC-INV-001: HSN codes per line — GST detail must cover the invoice's
        // lines exactly (1..N, no extras, no omissions).
        var covered = gst.LineDetails.Select(detail => detail.LineNumber).Order();

        if (!covered.SequenceEqual(Enumerable.Range(1, draft.Lines.Count)))
        {
            throw new InvoiceValidationException(
                "IN GST content must carry exactly one HSN-coded detail per invoice line (CC-INV-001).");
        }
    }
}
