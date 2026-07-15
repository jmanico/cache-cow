namespace CacheCow.Modules.CatalogInventory.Catalog;

/// <summary>
/// Serving estimate carried by every SKU (CC-CAT-001), modeled as servings per
/// package. The semantics of "serving estimate" (per person, per package,
/// per 100 g) are unspecified in CC-CAT-001 — an open question on issue 029
/// awaiting a human decision; per-package is the interim reading and the type
/// isolates the answer to one place. Zero and negative estimates are rejected.
/// </summary>
public readonly record struct ServingEstimate
{
    private ServingEstimate(int servingsPerPackage)
    {
        ServingsPerPackage = servingsPerPackage;
    }

    public int ServingsPerPackage { get; }

    public static ServingEstimate PerPackage(int servings) =>
        servings > 0
            ? new ServingEstimate(servings)
            : throw new ArgumentOutOfRangeException(
                nameof(servings), servings, "A serving estimate must be a positive count (CC-CAT-001).");
}
