using System.Diagnostics.CodeAnalysis;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.CatalogInventory.Catalog;

/// <summary>
/// The SKU aggregate of the Catalog &amp; Inventory bounded context
/// (ARCHITECTURE.md, "Server bounded contexts" 2), carrying every CC-CAT-001
/// structured field: unique ID, localized name, veg/non-veg classification,
/// cut/category, net weight, serving estimate, ingredients, allergens,
/// nutrition, storage and reheat instructions, and per-market availability
/// flags. Construction validates all invariants and rejects invalid input —
/// no partial or defaulted food data ever exists (issue 029 AC-01/AC-02;
/// SECURITY.md, Input validation rule 1). Allergen, nutrition, ingredient,
/// and classification data live only in these typed fields; the type carries
/// no CMS free-text channel to source or override them (CC-CAT-004, AC-04).
/// </summary>
public sealed class Sku
{
    private readonly Dictionary<Market, NutritionFacts> _nutritionByMarket;
    private readonly HashSet<Market> _availableMarkets;

    private Sku(
        SkuId id,
        LocalizedText name,
        ProductClassification classification,
        CutCategory cutCategory,
        NetWeight netWeight,
        ServingEstimate servingEstimate,
        IReadOnlyList<Ingredient> ingredients,
        IReadOnlySet<Allergen> allergens,
        Dictionary<Market, NutritionFacts> nutritionByMarket,
        LocalizedText storageInstructions,
        LocalizedText reheatInstructions,
        HashSet<Market> availableMarkets)
    {
        Id = id;
        Name = name;
        Classification = classification;
        CutCategory = cutCategory;
        NetWeight = netWeight;
        ServingEstimate = servingEstimate;
        Ingredients = ingredients;
        Allergens = allergens;
        _nutritionByMarket = nutritionByMarket;
        StorageInstructions = storageInstructions;
        ReheatInstructions = reheatInstructions;
        _availableMarkets = availableMarkets;
    }

    public SkuId Id { get; }

    public LocalizedText Name { get; }

    public ProductClassification Classification { get; }

    public CutCategory CutCategory { get; }

    public NetWeight NetWeight { get; }

    public ServingEstimate ServingEstimate { get; }

    public IReadOnlyList<Ingredient> Ingredients { get; }

    public IReadOnlySet<Allergen> Allergens { get; }

    public LocalizedText StorageInstructions { get; }

    public LocalizedText ReheatInstructions { get; }

    /// <summary>The markets whose availability flag is set (CC-CAT-001).</summary>
    public IReadOnlyCollection<Market> AvailableMarkets => _availableMarkets;

    public static Sku Create(
        SkuId id,
        LocalizedText name,
        ProductClassification classification,
        CutCategory cutCategory,
        NetWeight netWeight,
        ServingEstimate servingEstimate,
        IReadOnlyList<Ingredient> ingredients,
        IReadOnlySet<Allergen> allergens,
        IReadOnlyDictionary<Market, NutritionFacts> nutritionByMarket,
        LocalizedText storageInstructions,
        LocalizedText reheatInstructions,
        IReadOnlyCollection<Market> availableMarkets)
    {
        if (id == default)
        {
            throw new ArgumentException("A SKU requires an initialized unique ID (CC-CAT-001).", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(name);

        // Closed enumeration: no constructor path yields a SKU without a valid
        // classification (issue 029 AC-02; CC-MKT-003 dependency).
        if (!Enum.IsDefined(classification))
        {
            throw new ArgumentException(
                $"'{classification}' is outside the closed veg/non-veg enumeration (CC-CAT-001).",
                nameof(classification));
        }

        if (cutCategory == default)
        {
            throw new ArgumentException("A SKU requires an initialized cut/category (CC-CAT-001).", nameof(cutCategory));
        }

        if (netWeight.Grams <= 0)
        {
            throw new ArgumentException("A SKU requires a positive net weight (CC-CAT-001).", nameof(netWeight));
        }

        if (servingEstimate.ServingsPerPackage <= 0)
        {
            throw new ArgumentException("A SKU requires a serving estimate (CC-CAT-001).", nameof(servingEstimate));
        }

        ArgumentNullException.ThrowIfNull(ingredients);
        if (ingredients.Count == 0 || ingredients.Any(ingredient => ingredient is null))
        {
            throw new ArgumentException(
                "A SKU requires a non-empty structured ingredient list with no null entries (CC-CAT-001, CC-CAT-004).",
                nameof(ingredients));
        }

        ArgumentNullException.ThrowIfNull(allergens);
        if (allergens.Any(allergen => !Enum.IsDefined(allergen)))
        {
            throw new ArgumentException(
                "Allergens must come from the typed closed set (CC-CAT-004).", nameof(allergens));
        }

        ArgumentNullException.ThrowIfNull(storageInstructions);
        ArgumentNullException.ThrowIfNull(reheatInstructions);

        ArgumentNullException.ThrowIfNull(availableMarkets);
        var markets = new HashSet<Market>();
        foreach (var market in availableMarkets)
        {
            if (market == default)
            {
                throw new ArgumentException(
                    "Availability flags must be keyed by initialized markets (CC-CAT-001, CC-MKT-001).",
                    nameof(availableMarkets));
            }

            markets.Add(market);
        }

        ArgumentNullException.ThrowIfNull(nutritionByMarket);
        var nutrition = new Dictionary<Market, NutritionFacts>(nutritionByMarket.Count);
        foreach (var (market, facts) in nutritionByMarket)
        {
            if (market == default || facts is null)
            {
                throw new ArgumentException(
                    "Nutrition entries must map an initialized market to structured facts (CC-CAT-004).",
                    nameof(nutritionByMarket));
            }

            nutrition[market] = facts;
        }

        // Every market the SKU is offered in must have a resolvable structured
        // nutrition representation — the single compliance source (issue 029
        // AC-06; CC-CMP-004). Missing food data means not offerable, never
        // offered with defaults (fail closed, SECURITY.md Logging rule 2).
        foreach (var market in markets)
        {
            if (!nutrition.ContainsKey(market))
            {
                throw new ArgumentException(
                    $"SKU is flagged available in {market} but has no structured nutrition for that market (CC-CAT-004, CC-CMP-004).",
                    nameof(nutritionByMarket));
            }
        }

        return new Sku(
            id,
            name,
            classification,
            cutCategory,
            netWeight,
            servingEstimate,
            ingredients.ToArray(),
            new HashSet<Allergen>(allergens),
            nutrition,
            storageInstructions,
            reheatInstructions,
            markets);
    }

    /// <summary>
    /// Per-market availability flag, keyed by the kernel Market type
    /// (issue 029 AC-05). Fail closed: a market with no flag is not available.
    /// </summary>
    public bool IsAvailableIn(Market market) => market != default && _availableMarkets.Contains(market);

    /// <summary>
    /// Resolves the structured nutrition representation for a market
    /// (CC-CMP-004; issue 029 AC-06). Fail closed: no entry, no value —
    /// never a default panel.
    /// </summary>
    public bool TryGetNutrition(Market market, [MaybeNullWhen(false)] out NutritionFacts facts)
    {
        if (market == default)
        {
            facts = null;
            return false;
        }

        return _nutritionByMarket.TryGetValue(market, out facts);
    }
}
