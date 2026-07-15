using System.Diagnostics.CodeAnalysis;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.CatalogInventory.Catalog;

/// <summary>
/// Read port over the catalog's SKU set. The in-memory implementation backs
/// the domain services until the PostgreSQL persistence adapter lands
/// (issue 015 schema; ARCHITECTURE.md, "Cross-cutting" data stores).
/// </summary>
public interface ISkuCatalog
{
    /// <summary>All SKUs in the catalog master data (ungated; gating is upstream composition).</summary>
    IReadOnlyCollection<Sku> All();

    bool TryGet(SkuId id, [MaybeNullWhen(false)] out Sku sku);
}
