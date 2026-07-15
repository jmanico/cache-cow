using CacheCow.Modules.WholesaleB2B.Partners;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.WholesaleB2B.PriceLists;

/// <summary>
/// In-memory implementation of <see cref="IWholesalePriceLists"/> until the
/// durable PostgreSQL store lands (wholesale schema behind its own
/// least-privilege role over TLS, no consumer-context grants — SECURITY.md,
/// Secret handling rule 10; CC-SEC-021; issue 015).
///
/// Reads are tenant-scoped by construction: lookups key on the context's own
/// <see cref="PartnerTenantContext.PartnerId"/>, so there is no code path that
/// consults another partner's rows (CC-WHS-003; SECURITY.md, Authentication
/// rule 9). The write members (<see cref="Register"/>,
/// <see cref="SetPaymentTerms"/>) are the administration seam; which staff role
/// authors price lists and terms, and through which audited dashboard surface,
/// is an open decision (issue 050, Open Questions) — until it lands,
/// <see cref="Register"/> refuses to overwrite an existing list rather than
/// inventing update semantics.
/// </summary>
public sealed class InMemoryWholesalePriceLists : IWholesalePriceLists
{
    private readonly Lock _gate = new();
    private readonly Dictionary<(PartnerId Partner, Market Market), WholesalePriceList> _lists = [];
    private readonly Dictionary<PartnerId, PaymentTerms> _termsOverrides = [];

    /// <summary>Adds a price list keyed (owner, market); duplicate registration is rejected (see class docs).</summary>
    public void Register(WholesalePriceList priceList)
    {
        ArgumentNullException.ThrowIfNull(priceList);

        lock (_gate)
        {
            if (!_lists.TryAdd((priceList.Owner, priceList.Market), priceList))
            {
                throw new WholesaleValidationException(
                    $"A wholesale price list is already registered for this partner in market {priceList.Market.Code}; update semantics await the price-list-administration decision (issue 050, Open Questions).");
            }
        }
    }

    /// <summary>Sets the per-partner terms adjustment (CC-WHS-004: net-60 default, adjustable per partner).</summary>
    public void SetPaymentTerms(PartnerId partnerId, PaymentTerms terms)
    {
        if (partnerId == default)
        {
            throw new WholesaleValidationException(
                "A payment-terms adjustment requires a partner identity (CC-WHS-004).");
        }

        _ = terms.NetDays; // rejects an uninitialized value (fail closed)

        lock (_gate)
        {
            _termsOverrides[partnerId] = terms;
        }
    }

    public WholesalePriceList GetPriceList(PartnerTenantContext context, Market market)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Market authorization first, then existence — both denials are the
        // same exception so a caller cannot distinguish "not yours" from
        // "does not exist" (SECURITY.md, Authentication rule 9).
        if (!context.IsAuthorizedFor(market))
        {
            throw new WholesalePriceListUnavailableException();
        }

        lock (_gate)
        {
            return _lists.TryGetValue((context.PartnerId, market), out var list)
                ? list
                : throw new WholesalePriceListUnavailableException();
        }
    }

    public PaymentTerms GetPaymentTerms(PartnerTenantContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        lock (_gate)
        {
            return _termsOverrides.TryGetValue(context.PartnerId, out var terms)
                ? terms
                : PaymentTerms.Net60Default;
        }
    }
}
