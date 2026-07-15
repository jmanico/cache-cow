using CacheCow.Modules.Invoicing.Invoices;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.Invoicing.Rendering;

/// <summary>
/// Input to <see cref="IInvoicePdfRenderer"/>: exactly the structured invoice
/// aggregate and the rendering locale — nothing else. There is deliberately no
/// free-text, HTML, template-string, or CMS-content channel on this type:
/// invoice PDFs render server-side from structured data only (CC-INV-002;
/// issue 048, AC-01). Amounts are locale-formatted at render time
/// (`Intl.NumberFormat` server equivalent — CC-PRC-004; DESIGN.md §4.4),
/// including JPY zero-decimal and INR lakh/crore grouping.
/// </summary>
public sealed class InvoicePdfRenderRequest
{
    public InvoicePdfRenderRequest(Invoice invoice, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        _ = locale.Tag; // uninitialized Locale fails closed

        Invoice = invoice;
        Locale = locale;
    }

    /// <summary>The immutable issued invoice (issues 046/047) — the sole content source.</summary>
    public Invoice Invoice { get; }

    /// <summary>Rendering locale for strings and number formatting (CC-PRC-004; CC-I18N-003).</summary>
    public Locale Locale { get; }
}
