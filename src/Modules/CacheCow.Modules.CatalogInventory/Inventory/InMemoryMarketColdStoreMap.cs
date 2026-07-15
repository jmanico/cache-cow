using CacheCow.SharedKernel;

namespace CacheCow.Modules.CatalogInventory.Inventory;

/// <summary>
/// In-memory <see cref="IMarketColdStoreMap"/>: provisional default and test
/// double. Empty by default, which fails closed — every derivation resolves
/// unavailable-in-region until the host supplies the real topology
/// (issue 030, Failure Behavior). A store may serve multiple markets
/// (REQUIREMENTS.md §2; issue 030 AC-07).
/// </summary>
public sealed class InMemoryMarketColdStoreMap : IMarketColdStoreMap
{
    private readonly Dictionary<Market, HashSet<ColdStoreId>> _servingStores = [];

    public void Assign(Market market, ColdStoreId coldStoreId)
    {
        if (market == default)
        {
            throw new ArgumentException("An initialized market is required (CC-MKT-001).", nameof(market));
        }

        if (coldStoreId == default)
        {
            throw new ArgumentException("An initialized cold-store ID is required (CC-CAT-002).", nameof(coldStoreId));
        }

        if (!_servingStores.TryGetValue(market, out var stores))
        {
            stores = [];
            _servingStores[market] = stores;
        }

        stores.Add(coldStoreId);
    }

    public IReadOnlyCollection<ColdStoreId> StoresServing(Market market) =>
        market != default && _servingStores.TryGetValue(market, out var stores)
            ? stores
            : [];
}
