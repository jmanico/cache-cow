using CacheCow.Modules.CatalogInventory.Catalog;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.CatalogInventory.Tests;

internal static class TestLocales
{
    public static readonly Locale EnUs = Locale.Parse("en-US");
    public static readonly Locale EsEs = Locale.Parse("es-ES");
    public static readonly Locale EsMx = Locale.Parse("es-MX");
    public static readonly Locale DeDe = Locale.Parse("de-DE");
    public static readonly Locale JaJp = Locale.Parse("ja-JP");
    public static readonly Locale EnIn = Locale.Parse("en-IN");
    public static readonly Locale HiIn = Locale.Parse("hi-IN");
}

internal static class CatalogFixtures
{
    public static readonly NutritionFacts DefaultNutrition = NutritionFacts.Per100Grams(
        energyKilojoules: 1050m,
        energyKilocalories: 251m,
        fatGrams: 18.5m,
        saturatedFatGrams: 7.2m,
        carbohydrateGrams: 1.1m,
        sugarsGrams: 0.9m,
        proteinGrams: 21.3m,
        saltGrams: 1.8m);

    public static LocalizedText Text(params (Locale Locale, string Value)[] entries)
    {
        var map = new Dictionary<Locale, string>();
        foreach (var (locale, value) in entries)
        {
            map[locale] = value;
        }

        return LocalizedText.Create(map);
    }

    public static LocalizedText EnglishText(string value) => Text((TestLocales.EnUs, value));
}

/// <summary>
/// Builds a fully valid SKU (every CC-CAT-001 field populated) so each test
/// mutates exactly the field under test.
/// </summary>
internal sealed class SkuBuilder
{
    private readonly Dictionary<Locale, string> _names = new()
    {
        [TestLocales.EnUs] = "Test Smoked Paneer",
    };

    private string _id = "TEST-SKU-01";
    private ProductClassification _classification = ProductClassification.Vegetarian;
    private string _cutCategory = "smoked-cheese";
    private int _netWeightGrams = 500;
    private int _servingsPerPackage = 2;
    private IReadOnlyList<Ingredient> _ingredients =
        [new Ingredient(CatalogFixtures.EnglishText("Paneer"))];
    private IReadOnlySet<Allergen> _allergens = new HashSet<Allergen> { Allergen.Milk };
    private LocalizedText _storageInstructions = CatalogFixtures.EnglishText("Keep frozen at -18 °C.");
    private LocalizedText _reheatInstructions = CatalogFixtures.EnglishText("Oven 20 minutes at 180 °C.");
    private IReadOnlyCollection<Market> _availableMarkets = Market.All;
    private IReadOnlyDictionary<Market, NutritionFacts>? _nutritionOverride;

    public SkuBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    public SkuBuilder WithName(Locale locale, string name)
    {
        _names[locale] = name;
        return this;
    }

    public SkuBuilder WithClassification(ProductClassification classification)
    {
        _classification = classification;
        return this;
    }

    public SkuBuilder WithCutCategory(string cutCategory)
    {
        _cutCategory = cutCategory;
        return this;
    }

    public SkuBuilder WithNetWeightGrams(int grams)
    {
        _netWeightGrams = grams;
        return this;
    }

    public SkuBuilder WithServingsPerPackage(int servings)
    {
        _servingsPerPackage = servings;
        return this;
    }

    public SkuBuilder WithIngredients(IReadOnlyList<Ingredient> ingredients)
    {
        _ingredients = ingredients;
        return this;
    }

    public SkuBuilder WithAllergens(IReadOnlySet<Allergen> allergens)
    {
        _allergens = allergens;
        return this;
    }

    public SkuBuilder WithStorageInstructions(LocalizedText storageInstructions)
    {
        _storageInstructions = storageInstructions;
        return this;
    }

    public SkuBuilder WithReheatInstructions(LocalizedText reheatInstructions)
    {
        _reheatInstructions = reheatInstructions;
        return this;
    }

    public SkuBuilder WithAvailableMarkets(params Market[] markets)
    {
        _availableMarkets = markets;
        return this;
    }

    public SkuBuilder WithNutrition(IReadOnlyDictionary<Market, NutritionFacts> nutritionByMarket)
    {
        _nutritionOverride = nutritionByMarket;
        return this;
    }

    public Sku Build()
    {
        var nutrition = _nutritionOverride
            ?? _availableMarkets.Distinct().ToDictionary(
                market => market,
                _ => CatalogFixtures.DefaultNutrition);

        return Sku.Create(
            SkuId.Parse(_id),
            LocalizedText.Create(_names),
            _classification,
            CutCategory.Parse(_cutCategory),
            NetWeight.FromGrams(_netWeightGrams),
            ServingEstimate.PerPackage(_servingsPerPackage),
            _ingredients,
            _allergens,
            nutrition,
            _storageInstructions,
            _reheatInstructions,
            _availableMarkets);
    }
}
