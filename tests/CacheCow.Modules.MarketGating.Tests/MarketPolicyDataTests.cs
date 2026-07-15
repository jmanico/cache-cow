using CacheCow.Modules.MarketGating.Policy;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.MarketGating.Tests;

/// <summary>
/// Issue 023: the per-market policy table is declarative data covering exactly
/// the six launch markets, and lookups outside it fail closed.
/// </summary>
public sealed class MarketPolicyDataTests
{
    [Fact]
    [Requirement("CC-MKT-001")]
    [Requirement("CC-MKT-006")]
    public void Policy_table_covers_exactly_the_six_launch_markets()
    {
        var policyMarkets = LaunchMarketPolicies.All
            .Select(p => p.Market.Code)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var launchMarkets = Market.All
            .Select(m => m.Code)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["DE", "ES", "IN", "JP", "MX", "US"], policyMarkets);
        Assert.Equal(launchMarkets, policyMarkets);
    }

    [Theory]
    [Requirement("CC-PRC-001")]
    [InlineData("US", "USD")]
    [InlineData("ES", "EUR")]
    [InlineData("MX", "MXN")]
    [InlineData("DE", "EUR")]
    [InlineData("JP", "JPY")]
    [InlineData("IN", "INR")]
    public void Each_market_carries_its_fixed_launch_currency(string marketCode, string currencyCode)
    {
        Assert.True(LaunchMarketPolicies.TryGet(Market.Parse(marketCode), out var policy));
        Assert.Equal(currencyCode, policy!.Currency.Code);
    }

    [Theory]
    [Requirement("CC-PRC-002")]
    [InlineData("US", TaxDisplayConvention.TaxExclusiveEstimatedAtCheckout, false)]
    [InlineData("ES", TaxDisplayConvention.TaxInclusive, false)]
    [InlineData("MX", TaxDisplayConvention.TaxInclusive, false)]
    [InlineData("DE", TaxDisplayConvention.TaxInclusive, true)]
    [InlineData("JP", TaxDisplayConvention.TaxInclusive, false)]
    [InlineData("IN", TaxDisplayConvention.TaxInclusive, false)]
    public void Tax_display_convention_follows_market_convention(
        string marketCode, TaxDisplayConvention expected, bool unitPricePerKg)
    {
        Assert.True(LaunchMarketPolicies.TryGet(Market.Parse(marketCode), out var policy));
        Assert.Equal(expected, policy!.TaxDisplay);
        Assert.Equal(unitPricePerKg, policy.DisplaysUnitPricePerKilogram);
    }

    [Fact]
    [Requirement("CC-MKT-003")]
    [Requirement("CC-CNT-006")]
    public void IN_policy_encodes_veg_only_catalog_and_fssai_marking_as_data()
    {
        Assert.True(LaunchMarketPolicies.TryGet(Market.IN, out var policy));

        Assert.False(policy!.NonVegSkusPermitted);
        Assert.Equal(VegetarianMarking.FssaiRegulatoryMark, policy.VegMarking);
        Assert.True(policy.NonVegMarkProhibited);
    }

    [Fact]
    [Requirement("CC-MKT-005")]
    public void IN_policy_encodes_cuts_absent_and_cows_in_primary_navigation()
    {
        Assert.True(LaunchMarketPolicies.TryGet(Market.IN, out var policy));

        Assert.Equal(ContentPlacement.NotAvailable, policy!.MeetOurCutsPlacement);
        Assert.Equal(ContentPlacement.PrimaryNavigation, policy.MeetOurCowsPlacement);
    }

    [Fact]
    [Requirement("CC-MKT-005")]
    public void Every_non_IN_market_places_cows_under_our_story_and_cuts_as_standard_page()
    {
        foreach (var policy in LaunchMarketPolicies.All.Where(p => p.Market != Market.IN))
        {
            Assert.Equal(ContentPlacement.UnderOurStory, policy.MeetOurCowsPlacement);
            Assert.Equal(ContentPlacement.StandardPage, policy.MeetOurCutsPlacement);
        }
    }

    [Fact]
    [Requirement("CC-MKT-007")]
    public void US_ES_DE_policies_permit_the_full_catalog_including_non_veg()
    {
        foreach (var market in new[] { Market.US, Market.ES, Market.DE })
        {
            Assert.True(LaunchMarketPolicies.TryGet(market, out var policy));
            Assert.True(policy!.NonVegSkusPermitted, $"{market.Code} must carry the full catalog (CC-MKT-007).");
        }
    }

    [Fact]
    [Requirement("CC-CNT-005")]
    public void Legal_content_sets_include_the_base_documents_everywhere_and_DE_extras()
    {
        foreach (var policy in LaunchMarketPolicies.All)
        {
            Assert.Contains(LegalDocument.PrivacyPolicy, policy.LegalContentSet);
            Assert.Contains(LegalDocument.Terms, policy.LegalContentSet);
            Assert.Contains(LegalDocument.ShippingAndReturns, policy.LegalContentSet);

            var hasImpressum = policy.LegalContentSet.Contains(LegalDocument.Impressum);
            var hasWiderruf = policy.LegalContentSet.Contains(LegalDocument.Widerrufsbelehrung);
            if (policy.Market == Market.DE)
            {
                Assert.True(hasImpressum && hasWiderruf, "DE requires Impressum and Widerrufsbelehrung (CC-CNT-005).");
            }
            else
            {
                Assert.False(hasImpressum || hasWiderruf, $"{policy.Market.Code} must not carry DE-only legal documents.");
            }
        }
    }

    [Fact]
    [Requirement("CC-MKT-006")]
    public void Policy_lookup_for_an_unknown_market_fails_closed()
    {
        // default(Market) is the only representable non-launch market value;
        // the launch set itself is closed at the type level (CC-MKT-001).
        Assert.False(LaunchMarketPolicies.TryGet(default, out var policy));
        Assert.Null(policy);
    }

    [Fact]
    [Requirement("CC-MKT-006")]
    public void Unknown_content_experience_resolves_to_not_available()
    {
        Assert.True(LaunchMarketPolicies.TryGet(Market.US, out var policy));
        Assert.Equal(ContentPlacement.NotAvailable, policy!.PlacementOf((ContentExperience)99));
    }
}
