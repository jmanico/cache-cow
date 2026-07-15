using CacheCow.Modules.CatalogInventory.Catalog;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.CatalogInventory.Tests;

/// <summary>
/// Issue 029: the SKU aggregate carries every CC-CAT-001 structured field,
/// validates on construction, and rejects invalid food data instead of
/// sanitizing or defaulting it (SECURITY.md, Input validation rule 1).
/// </summary>
public sealed class SkuModelTests
{
    [Fact]
    [Requirement("CC-CAT-001")]
    public void A_valid_sku_carries_every_structured_field()
    {
        var sku = new SkuBuilder()
            .WithId("VEG-PANEER-01")
            .WithName(TestLocales.HiIn, "स्मोक्ड पनीर")
            .WithClassification(ProductClassification.Vegetarian)
            .WithCutCategory("smoked-cheese")
            .WithNetWeightGrams(450)
            .WithServingsPerPackage(3)
            .Build();

        Assert.Equal(SkuId.Parse("VEG-PANEER-01"), sku.Id);
        Assert.True(sku.Name.TryGet(TestLocales.HiIn, out var hindiName));
        Assert.Equal("स्मोक्ड पनीर", hindiName);
        Assert.Equal(ProductClassification.Vegetarian, sku.Classification);
        Assert.Equal(CutCategory.Parse("smoked-cheese"), sku.CutCategory);
        Assert.Equal(450, sku.NetWeight.Grams);
        Assert.Equal(3, sku.ServingEstimate.ServingsPerPackage);
        Assert.NotEmpty(sku.Ingredients);
        Assert.Contains(Allergen.Milk, sku.Allergens);
        Assert.True(sku.StorageInstructions.TryGet(TestLocales.EnUs, out _));
        Assert.True(sku.ReheatInstructions.TryGet(TestLocales.EnUs, out _));
        Assert.Equal(Market.All.Count, sku.AvailableMarkets.Count);
        Assert.True(sku.TryGetNutrition(Market.US, out _));
    }

    [Fact]
    [Requirement("CC-CAT-001")]
    public void An_uninitialized_sku_id_is_rejected()
    {
        var valid = new SkuBuilder().Build();

        var exception = Assert.Throws<ArgumentException>(() => Sku.Create(
            default,
            valid.Name,
            valid.Classification,
            valid.CutCategory,
            valid.NetWeight,
            valid.ServingEstimate,
            valid.Ingredients,
            valid.Allergens,
            new Dictionary<Market, NutritionFacts> { [Market.US] = CatalogFixtures.DefaultNutrition },
            valid.StorageInstructions,
            valid.ReheatInstructions,
            [Market.US]));

        Assert.Equal("id", exception.ParamName);
    }

    [Fact]
    [Requirement("CC-CAT-001")]
    public void A_missing_localized_name_is_rejected()
    {
        var valid = new SkuBuilder().Build();

        Assert.Throws<ArgumentNullException>(() => Sku.Create(
            valid.Id,
            null!,
            valid.Classification,
            valid.CutCategory,
            valid.NetWeight,
            valid.ServingEstimate,
            valid.Ingredients,
            valid.Allergens,
            new Dictionary<Market, NutritionFacts> { [Market.US] = CatalogFixtures.DefaultNutrition },
            valid.StorageInstructions,
            valid.ReheatInstructions,
            [Market.US]));
    }

    [Theory]
    [Requirement("CC-CAT-001")]
    [Requirement("CC-MKT-003")]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(99)]
    public void A_classification_outside_the_closed_enumeration_is_rejected(int outOfRange)
    {
        var builder = new SkuBuilder().WithClassification((ProductClassification)outOfRange);

        var exception = Assert.Throws<ArgumentException>(() => builder.Build());

        Assert.Equal("classification", exception.ParamName);
    }

    [Fact]
    [Requirement("CC-CAT-001")]
    [Requirement("CC-MKT-003")]
    public void The_classification_enumeration_is_closed_to_veg_and_non_veg_and_defaults_to_the_most_restrictive()
    {
        Assert.Equal(
            [ProductClassification.NonVegetarian, ProductClassification.Vegetarian],
            Enum.GetValues<ProductClassification>());

        // Fail closed: a defaulted classification gates as non-veg, never veg
        // (SECURITY.md, Logging rule 2).
        Assert.Equal(ProductClassification.NonVegetarian, default(ProductClassification));
    }

    [Theory]
    [Requirement("CC-CAT-001")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" brisket")]
    [InlineData("brisket ")]
    public void An_invalid_cut_category_is_rejected_not_sanitized(string invalid)
    {
        Assert.False(CutCategory.TryParse(invalid, out _));
        Assert.Throws<FormatException>(() => CutCategory.Parse(invalid));
    }

    [Theory]
    [Requirement("CC-CAT-001")]
    [InlineData(0)]
    [InlineData(-500)]
    public void A_non_positive_net_weight_is_rejected(int grams)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NetWeight.FromGrams(grams));
    }

    [Theory]
    [Requirement("CC-CAT-001")]
    [InlineData(0)]
    [InlineData(-2)]
    public void A_non_positive_serving_estimate_is_rejected(int servings)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ServingEstimate.PerPackage(servings));
    }

    [Fact]
    [Requirement("CC-CAT-001")]
    public void A_defaulted_net_weight_or_serving_estimate_cannot_slip_past_the_aggregate()
    {
        var valid = new SkuBuilder().Build();

        var weightRejection = Assert.Throws<ArgumentException>(() => Sku.Create(
            valid.Id, valid.Name, valid.Classification, valid.CutCategory,
            default, valid.ServingEstimate, valid.Ingredients, valid.Allergens,
            new Dictionary<Market, NutritionFacts> { [Market.US] = CatalogFixtures.DefaultNutrition },
            valid.StorageInstructions, valid.ReheatInstructions, [Market.US]));
        Assert.Equal("netWeight", weightRejection.ParamName);

        var servingRejection = Assert.Throws<ArgumentException>(() => Sku.Create(
            valid.Id, valid.Name, valid.Classification, valid.CutCategory,
            valid.NetWeight, default, valid.Ingredients, valid.Allergens,
            new Dictionary<Market, NutritionFacts> { [Market.US] = CatalogFixtures.DefaultNutrition },
            valid.StorageInstructions, valid.ReheatInstructions, [Market.US]));
        Assert.Equal("servingEstimate", servingRejection.ParamName);
    }

    [Fact]
    [Requirement("CC-CAT-001")]
    [Requirement("CC-CAT-004")]
    public void An_empty_or_null_holed_ingredient_list_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new SkuBuilder()
            .WithIngredients([])
            .Build());

        Assert.Throws<ArgumentException>(() => new SkuBuilder()
            .WithIngredients([new Ingredient(CatalogFixtures.EnglishText("Paneer")), null!])
            .Build());
    }

    [Fact]
    [Requirement("CC-CAT-004")]
    public void An_allergen_outside_the_typed_closed_set_is_rejected()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SkuBuilder()
            .WithAllergens(new HashSet<Allergen> { (Allergen)999 })
            .Build());

        Assert.Equal("allergens", exception.ParamName);
    }

    [Fact]
    [Requirement("CC-CAT-004")]
    [Requirement("CC-CMP-004")]
    public void Allergen_and_nutrition_surfaces_are_typed_structured_fields_not_free_text()
    {
        // CC-CAT-004: allergen and nutrition data render from structured
        // fields, never free-text CMS content. The aggregate's compile-time
        // shape is the control: allergens are a closed enum set and nutrition
        // is a typed numeric record — no string-typed channel exists to smuggle
        // CMS rich text into food-safety data (issue 029 AC-04).
        Assert.Equal(typeof(IReadOnlySet<Allergen>), typeof(Sku).GetProperty(nameof(Sku.Allergens))!.PropertyType);

        foreach (var property in typeof(NutritionFacts).GetProperties())
        {
            Assert.Equal(typeof(decimal), property.PropertyType);
        }
    }

    [Fact]
    [Requirement("CC-CAT-004")]
    public void Negative_nutrition_values_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NutritionFacts.Per100Grams(
            energyKilojoules: -1m,
            energyKilocalories: 0m,
            fatGrams: 0m,
            saturatedFatGrams: 0m,
            carbohydrateGrams: 0m,
            sugarsGrams: 0m,
            proteinGrams: 0m,
            saltGrams: 0m));

        Assert.Throws<ArgumentOutOfRangeException>(() => NutritionFacts.Per100Grams(
            energyKilojoules: 100m,
            energyKilocalories: 24m,
            fatGrams: 1m,
            saturatedFatGrams: 0.5m,
            carbohydrateGrams: 2m,
            sugarsGrams: 1m,
            proteinGrams: 3m,
            saltGrams: -0.1m));
    }

    [Fact]
    [Requirement("CC-CAT-001")]
    [Requirement("CC-CMP-004")]
    public void A_sku_offered_in_a_market_without_structured_nutrition_for_that_market_is_rejected()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SkuBuilder()
            .WithAvailableMarkets(Market.US, Market.DE)
            .WithNutrition(new Dictionary<Market, NutritionFacts>
            {
                [Market.US] = CatalogFixtures.DefaultNutrition,
            })
            .Build());

        Assert.Equal("nutritionByMarket", exception.ParamName);
    }

    [Fact]
    [Requirement("CC-CMP-004")]
    [Requirement("CC-CAT-004")]
    public void Nutrition_resolves_per_market_from_structured_data_and_fails_closed_elsewhere()
    {
        var sku = new SkuBuilder()
            .WithAvailableMarkets(Market.JP)
            .WithNutrition(new Dictionary<Market, NutritionFacts>
            {
                [Market.JP] = CatalogFixtures.DefaultNutrition,
            })
            .Build();

        Assert.True(sku.TryGetNutrition(Market.JP, out var facts));
        Assert.Equal(CatalogFixtures.DefaultNutrition.EnergyKilojoules, facts.EnergyKilojoules);

        // No entry, no value — never a defaulted panel (fail closed).
        Assert.False(sku.TryGetNutrition(Market.DE, out _));
        Assert.False(sku.TryGetNutrition(default, out _));
    }

    [Fact]
    [Requirement("CC-CAT-001")]
    public void Missing_storage_or_reheat_instructions_are_rejected()
    {
        var valid = new SkuBuilder().Build();

        Assert.Throws<ArgumentNullException>(() => Sku.Create(
            valid.Id, valid.Name, valid.Classification, valid.CutCategory,
            valid.NetWeight, valid.ServingEstimate, valid.Ingredients, valid.Allergens,
            new Dictionary<Market, NutritionFacts> { [Market.US] = CatalogFixtures.DefaultNutrition },
            null!, valid.ReheatInstructions, [Market.US]));

        Assert.Throws<ArgumentNullException>(() => Sku.Create(
            valid.Id, valid.Name, valid.Classification, valid.CutCategory,
            valid.NetWeight, valid.ServingEstimate, valid.Ingredients, valid.Allergens,
            new Dictionary<Market, NutritionFacts> { [Market.US] = CatalogFixtures.DefaultNutrition },
            valid.StorageInstructions, null!, [Market.US]));
    }

    [Fact]
    [Requirement("CC-CAT-001")]
    public void Per_market_availability_flags_are_keyed_by_market_and_fail_closed()
    {
        var sku = new SkuBuilder()
            .WithAvailableMarkets(Market.US, Market.DE)
            .WithNutrition(new Dictionary<Market, NutritionFacts>
            {
                [Market.US] = CatalogFixtures.DefaultNutrition,
                [Market.DE] = CatalogFixtures.DefaultNutrition,
            })
            .Build();

        Assert.True(sku.IsAvailableIn(Market.US));
        Assert.True(sku.IsAvailableIn(Market.DE));

        // No flag means not available — never defaulted to available.
        Assert.False(sku.IsAvailableIn(Market.IN));
        Assert.False(sku.IsAvailableIn(Market.JP));
        Assert.False(sku.IsAvailableIn(default));
    }

    [Fact]
    [Requirement("CC-CAT-001")]
    public void An_uninitialized_market_in_the_availability_flags_is_rejected()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SkuBuilder()
            .WithAvailableMarkets(Market.US, default)
            .Build());

        Assert.Equal("availableMarkets", exception.ParamName);
    }

    [Fact]
    [Requirement("CC-CAT-001")]
    [Requirement("CC-CAT-004")]
    public void Localized_text_rejects_empty_maps_blank_values_and_uninitialized_locales()
    {
        Assert.Throws<ArgumentException>(() =>
            LocalizedText.Create(new Dictionary<Locale, string>()));

        Assert.Throws<ArgumentException>(() =>
            LocalizedText.Create(new Dictionary<Locale, string> { [TestLocales.EnUs] = "  " }));

        Assert.Throws<ArgumentException>(() =>
            LocalizedText.Create(new Dictionary<Locale, string> { [default] = "text" }));
    }

    [Fact]
    [Requirement("CC-CAT-004")]
    public void Localized_lookup_is_exact_locale_and_fails_closed()
    {
        var text = CatalogFixtures.Text(
            (TestLocales.JaJp, "スモークパニール"),
            (TestLocales.HiIn, "स्मोक्ड पनीर"));

        Assert.True(text.TryGet(TestLocales.JaJp, out var japanese));
        Assert.Equal("スモークパニール", japanese);

        // No cross-locale fallback here: fallback policy is a Content &
        // Localization concern (issue 029, Open Questions).
        Assert.False(text.TryGet(TestLocales.EnUs, out _));
        Assert.False(text.TryGet(default, out _));
    }

    [Fact]
    [Requirement("CC-CAT-001")]
    public void The_in_memory_catalog_enforces_sku_id_uniqueness()
    {
        var catalog = new InMemorySkuCatalog();
        catalog.Add(new SkuBuilder().WithId("VEG-PANEER-01").Build());

        Assert.Throws<ArgumentException>(() =>
            catalog.Add(new SkuBuilder().WithId("VEG-PANEER-01").Build()));

        Assert.True(catalog.TryGet(SkuId.Parse("VEG-PANEER-01"), out _));
        Assert.False(catalog.TryGet(SkuId.Parse("MISSING"), out _));
        Assert.False(catalog.TryGet(default, out _));
        Assert.Single(catalog.All());
    }
}
