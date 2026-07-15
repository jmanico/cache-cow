namespace CacheCow.Modules.CatalogInventory.Catalog;

/// <summary>
/// Net weight of a SKU (CC-CAT-001), held canonically in metric grams;
/// per-market display conversion (US imperial-primary, CC-I18N-003) is a
/// presentation concern downstream. Zero and negative weights are rejected.
/// </summary>
public readonly record struct NetWeight
{
    private NetWeight(int grams)
    {
        Grams = grams;
    }

    public int Grams { get; }

    public static NetWeight FromGrams(int grams) =>
        grams > 0
            ? new NetWeight(grams)
            : throw new ArgumentOutOfRangeException(
                nameof(grams), grams, "Net weight must be a positive number of grams (CC-CAT-001).");
}
