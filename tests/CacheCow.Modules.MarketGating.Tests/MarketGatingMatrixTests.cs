using CacheCow.Modules.MarketGating.Enforcement;
using CacheCow.Modules.MarketGating.Policy;
using CacheCow.Modules.MarketGating.Resolution;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.MarketGating.Tests;

/// <summary>
/// Issue 027: the exhaustive market-gating matrix — every market × veg/non-veg
/// × every declared response surface — asserting CC-MKT-003/004/005/007
/// outcomes. Dimensions are derived from the policy data and the declared
/// enums, not hand-maintained lists (CC-MKT-006; ARCHITECTURE.md, Dependency
/// rule 7), while the requirement-fixed cells (IN exclusion, US/ES/DE full
/// catalog, veg everywhere) are asserted independently of the policy table so
/// a policy-data regression fails the matrix rather than being mirrored by it.
/// </summary>
public sealed class MarketGatingMatrixTests
{
    private static readonly MarketGatingService Gating = new();

    private static readonly SkuId VegSku = SkuId.Parse("VEG-PANEER-01");
    private static readonly SkuId NonVegSku = SkuId.Parse("BEEF-BRISKET-01");

    /// <summary>Markets whose full-catalog breadth is fixed by CC-MKT-007.</summary>
    private static readonly string[] FullCatalogMarkets = ["US", "ES", "DE"];

    private static TransactingContext Context(string marketCode) =>
        new(Market.Parse(marketCode), Locale.Parse("en-US"));

    public static TheoryData<string, SkuClassification, ResponseSurface> AllMatrixCells()
    {
        var cells = new TheoryData<string, SkuClassification, ResponseSurface>();
        foreach (var policy in LaunchMarketPolicies.All) // dimension derived from policy data (issue 027 AC-04)
        {
            foreach (var classification in Enum.GetValues<SkuClassification>())
            {
                foreach (var surface in Enum.GetValues<ResponseSurface>())
                {
                    cells.Add(policy.Market.Code, classification, surface);
                }
            }
        }

        return cells;
    }

    [Theory]
    [Requirement("CC-QA-003")]
    [Requirement("CC-MKT-003")]
    [Requirement("CC-MKT-006")]
    [Requirement("CC-MKT-007")]
    [MemberData(nameof(AllMatrixCells))]
    public void Every_market_classification_surface_cell_gates_correctly(
        string marketCode, SkuClassification classification, ResponseSurface surface)
    {
        var skuId = classification == SkuClassification.Vegetarian ? VegSku : NonVegSku;
        var decision = Gating.EvaluateSku(Context(marketCode), new SkuGatingSubject(skuId, classification), surface);

        if (classification == SkuClassification.Vegetarian)
        {
            // Vegetarian SKUs are available in every market (CC-MKT-007) —
            // asserted independently of the policy table.
            Assert.True(decision.IsAllowed,
                $"[{marketCode}×Veg×{surface}] veg SKUs must be available in all markets (CC-MKT-007).");
            return;
        }

        if (marketCode == "IN")
        {
            // Non-veg excluded from every IN surface (CC-MKT-003) — asserted
            // independently of the policy table.
            Assert.False(decision.IsAllowed,
                $"[IN×NonVeg×{surface}] non-veg SKUs must be excluded from every IN response (CC-MKT-003).");
            Assert.Equal(GatingOutcome.ExcludedPresentAsNotFound, decision.Outcome);
            return;
        }

        if (FullCatalogMarkets.Contains(marketCode, StringComparer.Ordinal))
        {
            // US/ES/DE carry the full catalog including non-veg (CC-MKT-007).
            Assert.True(decision.IsAllowed,
                $"[{marketCode}×NonVeg×{surface}] must carry the full catalog (CC-MKT-007).");
            return;
        }

        // JP/MX breadth is policy data, not a requirement (issue 025, Open
        // Questions): assert the decision is consistent with the declared policy.
        Assert.True(LaunchMarketPolicies.TryGet(Market.Parse(marketCode), out var policy));
        Assert.Equal(policy!.NonVegSkusPermitted, decision.IsAllowed);
    }

    public static TheoryData<string, ContentExperience> AllContentCells()
    {
        var cells = new TheoryData<string, ContentExperience>();
        foreach (var policy in LaunchMarketPolicies.All)
        {
            foreach (var experience in Enum.GetValues<ContentExperience>())
            {
                cells.Add(policy.Market.Code, experience);
            }
        }

        return cells;
    }

    [Theory]
    [Requirement("CC-QA-003")]
    [Requirement("CC-MKT-005")]
    [MemberData(nameof(AllContentCells))]
    public void Every_market_content_experience_cell_gates_correctly(string marketCode, ContentExperience experience)
    {
        var context = Context(marketCode);
        var decision = Gating.EvaluateContentExperience(context, experience);
        var placement = Gating.PlacementOf(context, experience);

        if (marketCode == "IN" && experience == ContentExperience.MeetOurCuts)
        {
            Assert.False(decision.IsAllowed, "Meet our Cuts must be unreachable in IN (CC-MKT-005).");
            Assert.Equal(GatingDecision.NotFoundStatusCode, decision.ExcludedHttpStatusCode);
            Assert.Equal(ContentPlacement.NotAvailable, placement);
            return;
        }

        Assert.True(decision.IsAllowed, $"[{marketCode}×{experience}] must be reachable (CC-MKT-005).");

        if (experience == ContentExperience.MeetOurCows)
        {
            var expected = marketCode == "IN" ? ContentPlacement.PrimaryNavigation : ContentPlacement.UnderOurStory;
            Assert.Equal(expected, placement); // CC-MKT-005 nav placement
        }
    }

    [Fact]
    [Requirement("CC-SEC-012")]
    public void Gating_outcome_is_identical_across_all_seven_locales()
    {
        // Locale drives strings, never gating: a user shopping IN in any
        // launch locale sees the same exclusions.
        foreach (var locale in LaunchLocales.All)
        {
            var context = new TransactingContext(Market.IN, locale);
            var decision = Gating.EvaluateSku(
                context, new SkuGatingSubject(NonVegSku, SkuClassification.NonVegetarian), ResponseSurface.ProductDetail);

            Assert.False(decision.IsAllowed);
        }
    }
}
