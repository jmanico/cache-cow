using CacheCow.Modules.MarketGating.Enforcement;
using CacheCow.Modules.MarketGating.Policy;
using CacheCow.Modules.MarketGating.Resolution;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.MarketGating.Tests;

/// <summary>
/// Issues 025/026: the enforcement point excludes server-side and fails
/// closed; exclusions are 404-as-data — never 403, never a redirect.
/// </summary>
public sealed class GatingEnforcementTests
{
    private static readonly MarketGatingService Gating = new();

    private static readonly TransactingContext IndiaContext = new(Market.IN, Locale.Parse("hi-IN"));
    private static readonly TransactingContext UsContext = new(Market.US, Locale.Parse("en-US"));

    private sealed record CatalogItem(SkuId SkuId, SkuClassification Classification);

    private static readonly CatalogItem Paneer = new(SkuId.Parse("VEG-PANEER-01"), SkuClassification.Vegetarian);
    private static readonly CatalogItem Jackfruit = new(SkuId.Parse("VEG-JACKFRUIT-01"), SkuClassification.Vegetarian);
    private static readonly CatalogItem Brisket = new(SkuId.Parse("BEEF-BRISKET-01"), SkuClassification.NonVegetarian);
    private static readonly CatalogItem Ribs = new(SkuId.Parse("PORK-RIBS-01"), SkuClassification.NonVegetarian);

    [Theory]
    [Requirement("CC-MKT-003")]
    [Requirement("CC-API-007")]
    [MemberData(nameof(AllSurfaces))]
    public void Server_side_filtering_removes_every_non_veg_item_from_IN_on_every_surface(ResponseSurface surface)
    {
        var catalog = new[] { Paneer, Brisket, Jackfruit, Ribs };

        var gated = Gating.FilterSkus(IndiaContext, catalog, item => item.Classification, surface);

        Assert.Equal([Paneer, Jackfruit], gated.Value);
        Assert.DoesNotContain(gated.Value, item => item.Classification == SkuClassification.NonVegetarian);
        Assert.Equal(IndiaContext, gated.Context);
    }

    public static TheoryData<ResponseSurface> AllSurfaces()
    {
        var data = new TheoryData<ResponseSurface>();
        foreach (var surface in Enum.GetValues<ResponseSurface>())
        {
            data.Add(surface);
        }

        return data;
    }

    [Fact]
    [Requirement("CC-MKT-007")]
    public void Full_catalog_markets_keep_non_veg_items_and_veg_filtering_stays_possible()
    {
        var catalog = new[] { Paneer, Brisket };

        var gated = Gating.FilterSkus(UsContext, catalog, item => item.Classification, ResponseSurface.CatalogListing);
        Assert.Equal(catalog, gated.Value);

        // The veg single-toggle filter (CC-CAT-006) composes on top: veg SKUs
        // are present and identifiable in every market's gated output.
        Assert.Contains(gated.Value, item => item.Classification == SkuClassification.Vegetarian);
    }

    [Fact]
    [Requirement("CC-MKT-004")]
    public void Excluded_resources_present_as_404_never_403_never_a_redirect()
    {
        var decision = Gating.EvaluateSku(
            IndiaContext, new SkuGatingSubject(Brisket.SkuId, Brisket.Classification), ResponseSurface.ProductDetail);

        Assert.False(decision.IsAllowed);
        Assert.Equal(404, decision.ExcludedHttpStatusCode);
        Assert.Equal(404, GatingDecision.NotFoundStatusCode);
        // The decision type carries no redirect target and no alternate status:
        // 403/redirect are unrepresentable (CC-MKT-004, encoded as data).
    }

    [Fact]
    [Requirement("CC-MKT-004")]
    public void Allowed_resources_carry_no_exclusion_status()
    {
        var decision = Gating.EvaluateSku(
            UsContext, new SkuGatingSubject(Brisket.SkuId, Brisket.Classification), ResponseSurface.ProductDetail);

        Assert.True(decision.IsAllowed);
        Assert.Null(decision.ExcludedHttpStatusCode);
    }

    [Fact]
    [Requirement("CC-MKT-003")]
    public void Unknown_sku_classification_fails_closed_to_exclusion()
    {
        var decision = Gating.EvaluateSku(
            IndiaContext, new SkuGatingSubject(SkuId.Parse("SKU-X"), (SkuClassification)99), ResponseSurface.Search);

        Assert.False(decision.IsAllowed);
        Assert.Equal(GatingDenialReason.UnknownSkuClassification, decision.DenialReason);
    }

    [Fact]
    [Requirement("CC-MKT-003")]
    public void Unknown_response_surface_fails_closed_to_exclusion()
    {
        var decision = Gating.EvaluateSku(
            UsContext, new SkuGatingSubject(Paneer.SkuId, Paneer.Classification), (ResponseSurface)99);

        Assert.False(decision.IsAllowed);
        Assert.Equal(GatingDenialReason.UnknownResponseSurface, decision.DenialReason);
    }

    [Fact]
    [Requirement("CC-MKT-003")]
    public void Missing_gating_input_fails_closed_to_exclusion()
    {
        Assert.False(Gating.EvaluateSku(null!, new SkuGatingSubject(Paneer.SkuId, Paneer.Classification), ResponseSurface.Search).IsAllowed);
        Assert.False(Gating.EvaluateSku(UsContext, null!, ResponseSurface.Search).IsAllowed);
        Assert.False(Gating.EvaluateContentExperience(null!, ContentExperience.MeetOurCows).IsAllowed);
        Assert.Equal(ContentPlacement.NotAvailable, Gating.PlacementOf(null!, ContentExperience.MeetOurCows));
    }

    [Fact]
    [Requirement("CC-MKT-003")]
    public void A_throwing_classification_selector_excludes_the_item_never_bypasses()
    {
        var catalog = new[] { Paneer, Brisket };

        var gated = Gating.FilterSkus<CatalogItem>(
            IndiaContext,
            catalog,
            item => item == Paneer ? item.Classification : throw new InvalidOperationException("catalog fault"),
            ResponseSurface.CatalogListing);

        Assert.Equal([Paneer], gated.Value); // the unclassifiable item is excluded, not admitted
    }

    [Fact]
    [Requirement("CC-MKT-005")]
    public void Content_experience_gating_fails_closed_on_unknown_experience()
    {
        var decision = Gating.EvaluateContentExperience(UsContext, (ContentExperience)99);

        Assert.False(decision.IsAllowed);
        Assert.Equal(GatingDenialReason.UnknownContentExperience, decision.DenialReason);
    }
}
