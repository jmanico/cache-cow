namespace CacheCow.Modules.MarketGating.Policy;

/// <summary>
/// Product classification driving regional gating and labeling
/// (REQUIREMENTS.md §2). Supplied by the caller per SKU — catalog data lives in
/// the Catalog &amp; Inventory context (issue 029), never here. The zero value is
/// deliberately <see cref="NonVegetarian"/> so an unset/defaulted classification
/// gates as the most-restrictive class (fail closed, SECURITY.md Logging rule 2).
/// </summary>
public enum SkuClassification
{
    /// <summary>Non-veg SKU: excluded server-side from every IN response (CC-MKT-003).</summary>
    NonVegetarian = 0,

    /// <summary>Veg SKU: available and filterable in all markets (CC-MKT-007).</summary>
    Vegetarian = 1,
}
