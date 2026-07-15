using CacheCow.SharedKernel;

namespace CacheCow.Modules.CatalogInventory.Inventory;

/// <summary>
/// Port for the market → cold-store serving relationship (CC-CAT-002).
/// Fulfillment topology is owned elsewhere (issue 044); this context only
/// consumes the mapping, so it is a port the host composes. How the mapping
/// is administered (static configuration vs. dashboard-managed data) is an
/// open question on issue 030 awaiting a human decision. A market with no
/// mapping yields an empty set — downstream derivation fails closed to
/// unavailable-in-region, never in-stock (issue 030, Failure Behavior).
/// </summary>
public interface IMarketColdStoreMap
{
    IReadOnlyCollection<ColdStoreId> StoresServing(Market market);
}
