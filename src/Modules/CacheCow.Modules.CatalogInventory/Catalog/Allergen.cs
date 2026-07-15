namespace CacheCow.Modules.CatalogInventory.Catalog;

/// <summary>
/// Typed allergen vocabulary (CC-CAT-001, CC-CAT-004: allergen data renders
/// from structured fields, never free text). The closed set is the union of
/// the regulated declaration lists across launch markets, anchored on the
/// EU FIC Annex II fourteen (which covers the US FDA major-allergen list;
/// wheat declares under <see cref="CerealsContainingGluten"/>). The exact
/// per-market declaration format (which subset, ordering, emphasis rules for
/// FDA, FSSAI, and JP labeling) is an open question on issue 029 awaiting a
/// compliance input; downstream per-market presentation selects from this
/// typed set (CC-CMP-004) — it never falls back to CMS free text.
/// </summary>
public enum Allergen
{
    CerealsContainingGluten = 0,
    Crustaceans = 1,
    Eggs = 2,
    Fish = 3,
    Peanuts = 4,
    Soybeans = 5,
    Milk = 6,
    TreeNuts = 7,
    Celery = 8,
    Mustard = 9,
    SesameSeeds = 10,
    Sulphites = 11,
    Lupin = 12,
    Molluscs = 13,
}
