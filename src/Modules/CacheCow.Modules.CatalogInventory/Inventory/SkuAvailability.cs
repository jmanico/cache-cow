namespace CacheCow.Modules.CatalogInventory.Inventory;

/// <summary>
/// Exactly the three user-facing stock states of CC-CAT-003 — a closed set,
/// no fourth state, no null, no free text (issue 030 AC-03). The cache-status
/// presentation vocabulary (CACHE HIT / WARMING / CACHE MISS, DESIGN.md §5.2)
/// maps 1:1 onto this enum downstream; the strings live in the presentation
/// layer, never here. The zero value is deliberately
/// <see cref="UnavailableInRegion"/> so a defaulted or unresolved state fails
/// closed — never in-stock (issue 030, Failure Behavior; SECURITY.md, Logging
/// rule 2).
/// </summary>
public enum SkuAvailability
{
    /// <summary>Unavailable in region: no purchase or preorder offered (CC-CAT-003).</summary>
    UnavailableInRegion = 0,

    /// <summary>Restocking: preorder permitted (CC-CAT-003).</summary>
    Restocking = 1,

    /// <summary>In stock: ships from the regional cold store (CC-CAT-003).</summary>
    InStock = 2,
}
