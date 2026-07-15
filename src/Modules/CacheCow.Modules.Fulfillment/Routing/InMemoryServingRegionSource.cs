using CacheCow.SharedKernel;

namespace CacheCow.Modules.Fulfillment.Routing;

/// <summary>
/// In-memory serving-region data, provisional until the real operational
/// source and its administration are decided (issue 044, Open Questions —
/// production persistence is additionally AT RISK on the residency/write-region
/// conflict, ARCHITECTURE.md "Known unknowns"). Empty by default, so with no
/// supplied data every routing request fails closed.
/// </summary>
public sealed class InMemoryServingRegionSource : IServingRegionSource
{
    private readonly Dictionary<Market, Dictionary<string, ColdStoreId>> _servingStoreByMarketPostal = [];
    private readonly HashSet<ColdStoreId> _knownStores = [];

    public InMemoryServingRegionSource(IEnumerable<ServingRegionEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        foreach (var entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry);

            if (!_servingStoreByMarketPostal.TryGetValue(entry.Market, out var byPostal))
            {
                byPostal = new Dictionary<string, ColdStoreId>(StringComparer.Ordinal);
                _servingStoreByMarketPostal.Add(entry.Market, byPostal);
            }

            if (byPostal.TryGetValue(entry.PostalCode.Value, out var existing) && existing != entry.Store)
            {
                throw new ArgumentException(
                    $"Ambiguous serving-region data: {entry.Market}/{entry.PostalCode} maps to both "
                    + $"'{existing}' and '{entry.Store}' (CC-FUL-001 requires the serving store).",
                    nameof(entries));
            }

            byPostal[entry.PostalCode.Value] = entry.Store;
            _knownStores.Add(entry.Store);
        }
    }

    public bool ServesMarket(Market market) => _servingStoreByMarketPostal.ContainsKey(market);

    public ColdStoreId? FindServingStore(Market market, PostalCode postalCode) =>
        _servingStoreByMarketPostal.TryGetValue(market, out var byPostal)
            && byPostal.TryGetValue(postalCode.Value, out var store)
            ? store
            : null;

    public bool IsKnownStore(ColdStoreId store) => _knownStores.Contains(store);
}
