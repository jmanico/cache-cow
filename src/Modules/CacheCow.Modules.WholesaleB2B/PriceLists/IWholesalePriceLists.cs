using CacheCow.Modules.WholesaleB2B.Partners;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.WholesaleB2B.PriceLists;

/// <summary>
/// The only read surface for wholesale prices and terms (CC-WHS-001/003/004).
/// Every member requires a <see cref="PartnerTenantContext"/> — the tenant-scoped
/// B2B identity that exists only for approved partners — and every lookup is
/// scoped to that context's <see cref="PartnerTenantContext.PartnerId"/> from
/// server-side state. By construction there is no member accepting a consumer
/// session, a guest, an anonymous caller, or a caller-supplied partner/price-list
/// identifier, so wholesale data is not derivable from consumer surfaces and one
/// partner can never read another's data (CC-WHS-003; SECURITY.md,
/// Authentication rules 8–9; ARCHITECTURE.md, Dependency rule 3). Verified by
/// the API-surface tests required by CC-QA-005.
///
/// Denials are <see cref="WholesalePriceListUnavailableException"/>, identical
/// for "does not exist", "belongs to another partner", and "market not
/// authorized" — the HTTP surface maps it to 404 (SECURITY.md, Authentication
/// rule 9). Wholesale responses are personalized and are never edge-cached
/// (`Cache-Control: no-store`; SECURITY.md, HTTP boundary rule 10) — the HTTP
/// layer is host scope (issues 052/053).
/// </summary>
public interface IWholesalePriceLists
{
    /// <summary>The requesting partner's own price list for one of its authorized markets, or a fail-closed denial.</summary>
    WholesalePriceList GetPriceList(PartnerTenantContext context, Market market);

    /// <summary>
    /// The requesting partner's payment terms: the per-partner adjustment when
    /// one exists, otherwise the ratified net-60 default (CC-WHS-004).
    /// </summary>
    PaymentTerms GetPaymentTerms(PartnerTenantContext context);
}
