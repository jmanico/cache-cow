namespace CacheCow.Modules.CatalogInventory.Catalog;

/// <summary>
/// A single structured ingredient (CC-CAT-001). Ingredients are an ordered
/// list of typed entries with localized names — never a free-text CMS blob
/// (CC-CAT-004; issue 029 AC-04).
/// </summary>
public sealed class Ingredient
{
    public Ingredient(LocalizedText name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
    }

    public LocalizedText Name { get; }
}
