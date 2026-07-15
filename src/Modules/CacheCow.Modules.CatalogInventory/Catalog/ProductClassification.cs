namespace CacheCow.Modules.CatalogInventory.Catalog;

/// <summary>
/// Veg/non-veg classification carried by every SKU (CC-CAT-001; REQUIREMENTS.md
/// §2) — the datum every market-gating decision keys on (CC-MKT-003; issue 029).
/// Closed enumeration: any value outside it is rejected at construction
/// (issue 029, AC-02). Owned by the Catalog &amp; Inventory context; the Market
/// &amp; Gating context has its own gating-subject classification type — contexts
/// do not share domain types beyond the kernel (ARCHITECTURE.md, Dependency
/// rule 9). The zero value is deliberately <see cref="NonVegetarian"/> so an
/// unset/defaulted classification is treated as the most-restrictive class
/// (fail closed, SECURITY.md Logging rule 2).
/// </summary>
public enum ProductClassification
{
    /// <summary>Non-veg SKU: excluded server-side from every IN response (CC-MKT-003).</summary>
    NonVegetarian = 0,

    /// <summary>Veg SKU: available and filterable in all markets (CC-MKT-007, CC-CAT-006).</summary>
    Vegetarian = 1,
}
