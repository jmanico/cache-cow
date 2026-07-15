using CacheCow.SharedKernel;

namespace CacheCow.Modules.Fulfillment.Routing;

/// <summary>
/// Port over the per-market serving-region data: which regional cold store
/// serves a delivery postal code in a market (CC-FUL-001). The topology itself
/// — which stores exist and which postal regions each serves — is operational
/// data that must be supplied, not invented (issue 044, Open Questions); the
/// host wires the real source. Implementations answer only from server-held
/// data, never from client hints (SECURITY.md, Authentication rule 10).
/// </summary>
public interface IServingRegionSource
{
    /// <summary>Whether any serving-region data exists for the market.</summary>
    bool ServesMarket(Market market);

    /// <summary>
    /// The regional cold store serving the delivery postal code in the market,
    /// or null when no store serves it. Callers fail closed on null.
    /// </summary>
    ColdStoreId? FindServingStore(Market market, PostalCode postalCode);

    /// <summary>Whether the store exists in the serving-region data at all.</summary>
    bool IsKnownStore(ColdStoreId store);
}
