using System.Diagnostics.CodeAnalysis;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.CatalogInventory.Catalog;

/// <summary>
/// In-memory <see cref="ISkuCatalog"/>: the provisional default and test
/// double until the PostgreSQL persistence adapter lands (issue 015). SKU IDs
/// are unique (CC-CAT-001); adding a duplicate is rejected, not overwritten.
/// </summary>
public sealed class InMemorySkuCatalog : ISkuCatalog
{
    private readonly Dictionary<SkuId, Sku> _skus = [];

    public void Add(Sku sku)
    {
        ArgumentNullException.ThrowIfNull(sku);
        if (!_skus.TryAdd(sku.Id, sku))
        {
            throw new ArgumentException($"A SKU with ID '{sku.Id}' already exists (CC-CAT-001).", nameof(sku));
        }
    }

    public IReadOnlyCollection<Sku> All() => _skus.Values;

    public bool TryGet(SkuId id, [MaybeNullWhen(false)] out Sku sku)
    {
        if (id == default)
        {
            sku = null;
            return false;
        }

        return _skus.TryGetValue(id, out sku);
    }
}
