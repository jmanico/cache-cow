using CacheCow.SharedKernel;

namespace CacheCow.Modules.WholesaleB2B.Partners;

/// <summary>
/// The tenant-scoped B2B session identity (CC-WHS-003, CC-API-004): the only
/// key that unlocks wholesale data. Every wholesale read API requires one, and
/// no API accepting a consumer, guest, or anonymous context exists — consumer
/// sessions cannot even express a wholesale request at the type level
/// (ARCHITECTURE.md, Dependency rule 3: wholesale prices/terms are unreachable
/// from consumer session context).
///
/// A context exists only for an <see cref="PartnerOnboardingState.Approved"/>
/// tenant: <see cref="ForApprovedTenant"/> is the sole factory and fails closed
/// for every other state, so pending, rejected, and suspended partners have no
/// wholesale surface (issue 049, AC-01/AC-05). It is a snapshot of server-side
/// tenancy state — production callers (portal sessions, issue 051; B2B tokens,
/// issues 054/055) mint it per request from the persisted tenant, so a
/// suspension is effective on the next request.
/// </summary>
public sealed class PartnerTenantContext
{
    private readonly HashSet<Market> _authorizedMarkets;

    private PartnerTenantContext(PartnerId partnerId, HashSet<Market> authorizedMarkets)
    {
        PartnerId = partnerId;
        _authorizedMarkets = authorizedMarkets;
    }

    /// <summary>The single tenant every query through this context is scoped to (SECURITY.md, Authentication rules 8–9).</summary>
    public PartnerId PartnerId { get; }

    /// <summary>The markets the tenant captured business identity for (CC-WHS-002, CC-API-007).</summary>
    public IReadOnlyCollection<Market> AuthorizedMarkets => _authorizedMarkets;

    /// <summary>
    /// Mints the wholesale access context for an approved tenant; throws
    /// <see cref="PartnerNotApprovedException"/> for any other state (fail
    /// closed — a tenant whose state cannot be resolved is not approved).
    /// </summary>
    public static PartnerTenantContext ForApprovedTenant(PartnerTenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        if (tenant.State != PartnerOnboardingState.Approved)
        {
            throw new PartnerNotApprovedException(tenant.State);
        }

        return new PartnerTenantContext(tenant.Id, [.. tenant.AuthorizedMarkets]);
    }

    public bool IsAuthorizedFor(Market market) => _authorizedMarkets.Contains(market);
}
