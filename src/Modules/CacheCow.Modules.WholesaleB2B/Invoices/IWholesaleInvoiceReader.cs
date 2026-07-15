using CacheCow.Modules.WholesaleB2B.Partners;

namespace CacheCow.Modules.WholesaleB2B.Invoices;

/// <summary>
/// A partner-visible invoice projection for the `invoices:read` surface:
/// identifiers, money, and status only — no address or contact PII travels
/// through the B2B read path (parallels CC-ORD-007's minimality principle).
/// Money is integer minor units plus ISO currency code (CC-PRC-003).
/// </summary>
public sealed record WholesaleInvoiceSummary(
    string InvoiceId,
    string OrderId,
    string CurrencyCode,
    long TotalMinorUnits,
    string Status);

/// <summary>
/// Port to the Invoicing bounded context (CC-INV-001; ARCHITECTURE.md,
/// Dependency rule 9 — cross-context needs are ports). The host adapts this to
/// the Invoicing module's issued-invoice store.
///
/// Contract: implementations MUST resolve strictly through
/// <see cref="PartnerTenantContext.PartnerId"/> — return the invoice only when
/// it belongs to that partner, and null for everything else, so "not found"
/// and "not yours" are indistinguishable and surface as 404 (CC-API-004;
/// SECURITY.md, Authentication rule 9). Issued invoices are immutable;
/// corrections are credit notes (CC-INV-001) — this port is read-only by
/// construction.
/// </summary>
public interface IWholesaleInvoiceReader
{
    WholesaleInvoiceSummary? FindInvoice(PartnerTenantContext context, string invoiceId);
}
