namespace CacheCow.Modules.CatalogInventory.Catalog;

/// <summary>
/// Structured nutrition declaration per 100 g (CC-CAT-001, CC-CAT-004): typed
/// numeric fields only, never free-text CMS content. The field set is the
/// EU FIC mandatory declaration (energy, fat, saturates, carbohydrate, sugars,
/// protein, salt); it is the structured single source from which every
/// market's panel renders (CC-CMP-004). The exact per-market panel schema —
/// field list, units, and rounding for FDA, FSSAI, and JP labeling — is an
/// open question on issue 029 awaiting a compliance input; those formats
/// resolve from this structured data downstream, never from CMS text.
/// Exact decimal values; negatives are rejected.
/// </summary>
public sealed class NutritionFacts
{
    private NutritionFacts(
        decimal energyKilojoules,
        decimal energyKilocalories,
        decimal fatGrams,
        decimal saturatedFatGrams,
        decimal carbohydrateGrams,
        decimal sugarsGrams,
        decimal proteinGrams,
        decimal saltGrams)
    {
        EnergyKilojoules = energyKilojoules;
        EnergyKilocalories = energyKilocalories;
        FatGrams = fatGrams;
        SaturatedFatGrams = saturatedFatGrams;
        CarbohydrateGrams = carbohydrateGrams;
        SugarsGrams = sugarsGrams;
        ProteinGrams = proteinGrams;
        SaltGrams = saltGrams;
    }

    public decimal EnergyKilojoules { get; }

    public decimal EnergyKilocalories { get; }

    public decimal FatGrams { get; }

    public decimal SaturatedFatGrams { get; }

    public decimal CarbohydrateGrams { get; }

    public decimal SugarsGrams { get; }

    public decimal ProteinGrams { get; }

    public decimal SaltGrams { get; }

    public static NutritionFacts Per100Grams(
        decimal energyKilojoules,
        decimal energyKilocalories,
        decimal fatGrams,
        decimal saturatedFatGrams,
        decimal carbohydrateGrams,
        decimal sugarsGrams,
        decimal proteinGrams,
        decimal saltGrams)
    {
        RequireNonNegative(energyKilojoules, nameof(energyKilojoules));
        RequireNonNegative(energyKilocalories, nameof(energyKilocalories));
        RequireNonNegative(fatGrams, nameof(fatGrams));
        RequireNonNegative(saturatedFatGrams, nameof(saturatedFatGrams));
        RequireNonNegative(carbohydrateGrams, nameof(carbohydrateGrams));
        RequireNonNegative(sugarsGrams, nameof(sugarsGrams));
        RequireNonNegative(proteinGrams, nameof(proteinGrams));
        RequireNonNegative(saltGrams, nameof(saltGrams));

        return new NutritionFacts(
            energyKilojoules,
            energyKilocalories,
            fatGrams,
            saturatedFatGrams,
            carbohydrateGrams,
            sugarsGrams,
            proteinGrams,
            saltGrams);
    }

    private static void RequireNonNegative(decimal value, string parameterName)
    {
        if (value < 0m)
        {
            throw new ArgumentOutOfRangeException(
                parameterName, value, "Nutrition values cannot be negative (CC-CAT-004).");
        }
    }
}
