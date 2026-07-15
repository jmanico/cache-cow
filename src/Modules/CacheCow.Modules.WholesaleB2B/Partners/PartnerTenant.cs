using CacheCow.SharedKernel;

namespace CacheCow.Modules.WholesaleB2B.Partners;

/// <summary>
/// The partner tenancy record (CC-WHS-002): the authorization boundary behind
/// which wholesale prices, terms, orders, and invoices live (CC-WHS-003,
/// CC-API-004; ARCHITECTURE.md, Dependency rule 3).
///
/// Self-service activation is unrepresentable at the type level (issue 049,
/// AC-04): <see cref="Create"/> is the only public constructor path and always
/// yields <see cref="PartnerOnboardingState.Draft"/>; the state cannot be set
/// from outside — every transition flows through
/// <see cref="PartnerOnboardingWorkflow"/>, whose actions all require a
/// <see cref="DashboardActorProof"/> and are audited append-before-effect.
/// Partner-supplied payloads can therefore never carry tenancy state
/// (SECURITY.md, Input validation rule 3: server-controlled fields come from
/// server state only).
///
/// Business identity is captured per market (CC-WHS-002); whether one tenant
/// spans multiple markets or tenancy is strictly per market is ambiguous in the
/// specs (issue 049, Open Questions), so this model accepts one identity per
/// market without foreclosing either reading — a single-market tenant simply
/// carries one identity.
/// </summary>
public sealed class PartnerTenant
{
    private const int MaxLegalNameLength = 256;

    private readonly Dictionary<Market, BusinessIdentity> _identitiesByMarket;

    private PartnerTenant(
        PartnerId id,
        string legalName,
        Dictionary<Market, BusinessIdentity> identitiesByMarket,
        PartnerOnboardingState state)
    {
        Id = id;
        LegalName = legalName;
        _identitiesByMarket = identitiesByMarket;
        State = state;
    }

    public PartnerId Id { get; }

    public string LegalName { get; }

    /// <summary>Server-controlled tenancy state; no public setter exists by design (issue 049, AC-07).</summary>
    public PartnerOnboardingState State { get; }

    /// <summary>Only <see cref="PartnerOnboardingState.Approved"/> is active (issue 049, AC-01).</summary>
    public bool IsActive => State == PartnerOnboardingState.Approved;

    /// <summary>The markets this tenant captured business identity for (CC-WHS-002).</summary>
    public IReadOnlyCollection<Market> AuthorizedMarkets => _identitiesByMarket.Keys;

    public IReadOnlyCollection<BusinessIdentity> BusinessIdentities => _identitiesByMarket.Values;

    /// <summary>
    /// Creates a new tenancy record, always in <see cref="PartnerOnboardingState.Draft"/>.
    /// There is deliberately no way to construct a tenant in any other state.
    /// </summary>
    public static PartnerTenant Create(
        PartnerId id,
        string legalName,
        IEnumerable<BusinessIdentity> businessIdentities)
    {
        if (id == default)
        {
            throw new WholesaleValidationException(
                "A partner tenant requires a partner identity (CC-WHS-002).");
        }

        if (string.IsNullOrWhiteSpace(legalName) || legalName.Length > MaxLegalNameLength)
        {
            throw new WholesaleValidationException(
                $"A partner tenant requires a non-empty legal name of at most {MaxLegalNameLength} characters (CC-WHS-002).");
        }

        ArgumentNullException.ThrowIfNull(businessIdentities);

        var byMarket = new Dictionary<Market, BusinessIdentity>();
        foreach (var identity in businessIdentities)
        {
            ArgumentNullException.ThrowIfNull(identity);
            if (!byMarket.TryAdd(identity.Market, identity))
            {
                throw new WholesaleValidationException(
                    $"Duplicate business identity for market {identity.Market.Code}; a tenant carries at most one identity per market (CC-WHS-002).");
            }
        }

        if (byMarket.Count == 0)
        {
            throw new WholesaleValidationException(
                "A partner tenant requires business identity for at least one market (CC-WHS-002, AC-03).");
        }

        return new PartnerTenant(id, legalName, byMarket, PartnerOnboardingState.Draft);
    }

    public bool IsAuthorizedFor(Market market) => _identitiesByMarket.ContainsKey(market);

    public bool TryGetBusinessIdentity(Market market, out BusinessIdentity? identity) =>
        _identitiesByMarket.TryGetValue(market, out identity);

    /// <summary>
    /// Internal by design: only <see cref="PartnerOnboardingWorkflow"/> — after
    /// legality checks, a <see cref="DashboardActorProof"/>, and a durable audit
    /// append — may produce a tenant in a new state (issue 049, AC-02/AC-04).
    /// </summary>
    internal PartnerTenant WithState(PartnerOnboardingState state) =>
        new(Id, LegalName, _identitiesByMarket, state);
}
