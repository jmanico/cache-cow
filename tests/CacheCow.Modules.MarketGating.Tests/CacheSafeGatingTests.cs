using CacheCow.Modules.MarketGating.Caching;
using CacheCow.Modules.MarketGating.Enforcement;
using CacheCow.Modules.MarketGating.Policy;
using CacheCow.Modules.MarketGating.Resolution;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.MarketGating.Tests;

/// <summary>
/// Issue 028: cache keys derive only from server-side transacting state,
/// personalized responses are never cacheable, unresolved contexts fail closed
/// to no-store, and SSR transfer state accepts only data gated for its exact
/// response.
/// </summary>
public sealed class CacheSafeGatingTests
{
    private static readonly TransactingContext GermanyGerman = new(Market.DE, Locale.Parse("de-DE"));
    private static readonly TransactingContext IndiaHindi = new(Market.IN, Locale.Parse("hi-IN"));

    [Fact]
    [Requirement("CC-MKT-009")]
    [Requirement("CC-SEC-013")]
    public void Cache_keys_derive_only_from_transacting_market_and_locale()
    {
        // By construction the only input is TransactingContext — there is no
        // API accepting Accept-Language, geolocation, or cookies. Two requests
        // differing only in client hints share one context, hence one key.
        var first = GatedCacheKey.FromContext(new TransactingContext(Market.DE, Locale.Parse("de-DE")));
        var second = GatedCacheKey.FromContext(new TransactingContext(Market.DE, Locale.Parse("de-DE")));

        Assert.Equal(first, second);
        Assert.Equal("market=DE|locale=de-DE", first.Value);
    }

    [Fact]
    [Requirement("CC-MKT-009")]
    public void Different_markets_or_locales_never_share_a_cache_key()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var market in Market.All)
        {
            foreach (var locale in LaunchLocales.All)
            {
                Assert.True(keys.Add(GatedCacheKey.FromContext(new TransactingContext(market, locale)).Value));
            }
        }

        Assert.Equal(Market.All.Count * LaunchLocales.All.Count, keys.Count);
    }

    [Theory]
    [Requirement("CC-MKT-009")]
    [Requirement("CC-SEC-013")]
    [InlineData(ResponseClass.PerUserPersonalized)]
    [InlineData(ResponseClass.Authenticated)]
    [InlineData(ResponseClass.Cart)]
    [InlineData(ResponseClass.Checkout)]
    public void Personalized_and_authenticated_responses_are_never_cacheable(ResponseClass responseClass)
    {
        var directive = ResponseCachePolicy.Classify(responseClass, GermanyGerman);

        Assert.False(directive.MayBeCached);
        Assert.Null(directive.Key);
        Assert.Same(CacheDirective.NoStore, directive);
        Assert.Equal("no-store", CacheDirective.NoStoreHeaderValue);
    }

    [Fact]
    [Requirement("CC-MKT-009")]
    public void Anonymous_gated_responses_are_cacheable_only_under_the_market_locale_key()
    {
        var directive = ResponseCachePolicy.Classify(ResponseClass.AnonymousMarketGated, IndiaHindi);

        Assert.True(directive.MayBeCached);
        Assert.Equal("market=IN|locale=hi-IN", directive.Key!.Value);
    }

    [Fact]
    [Requirement("CC-SEC-013")]
    public void Unresolved_gating_context_fails_closed_to_no_store()
    {
        // Never cached under a guessed or default key (issue 028 AC-05).
        var directive = ResponseCachePolicy.Classify(ResponseClass.AnonymousMarketGated, gatingContext: null);

        Assert.False(directive.MayBeCached);
    }

    [Fact]
    [Requirement("CC-SEC-013")]
    public void Undeclared_response_class_fails_closed_to_no_store()
    {
        var directive = ResponseCachePolicy.Classify((ResponseClass)99, GermanyGerman);

        Assert.False(directive.MayBeCached);
    }

    [Fact]
    [Requirement("CC-MKT-009")]
    public void Gated_404_responses_key_by_market_and_locale_like_any_gated_response()
    {
        // An IN 404 for a non-veg URL is never replayable to a US session:
        // the keys differ (issue 028 AC-06).
        var indiaKey = ResponseCachePolicy.Classify(ResponseClass.AnonymousMarketGated, IndiaHindi).Key;
        var usKey = ResponseCachePolicy.Classify(
            ResponseClass.AnonymousMarketGated, new TransactingContext(Market.US, Locale.Parse("en-US"))).Key;

        Assert.NotEqual(indiaKey, usKey);
    }

    [Fact]
    [Requirement("CC-MKT-009")]
    [Requirement("CC-SEC-013")]
    public void Transfer_state_accepts_only_payloads_gated_for_its_exact_context()
    {
        var gating = new MarketGatingService();
        var dePayload = gating.FilterSkus(
            GermanyGerman,
            new[] { ("BEEF-BRISKET-01", SkuClassification.NonVegetarian) },
            item => item.Item2,
            ResponseSurface.CatalogListing);

        var indiaResponse = SsrTransferState.For(IndiaHindi);

        // Data gated for DE cannot enter an IN response's hydration state.
        Assert.Throws<InvalidOperationException>(() => indiaResponse.Set("catalog", dePayload));
        Assert.Empty(indiaResponse.Entries);
    }

    [Fact]
    [Requirement("CC-MKT-009")]
    [Requirement("CC-MKT-003")]
    public void IN_transfer_state_carries_only_the_veg_filtered_payload()
    {
        var gating = new MarketGatingService();
        var catalog = new[]
        {
            ("VEG-PANEER-01", SkuClassification.Vegetarian),
            ("BEEF-BRISKET-01", SkuClassification.NonVegetarian),
        };

        var gated = gating.FilterSkus(IndiaHindi, catalog, item => item.Item2, ResponseSurface.CatalogListing);
        var transferState = SsrTransferState.For(IndiaHindi);
        transferState.Set("catalog", gated);

        var serialized = Assert.IsAssignableFrom<IReadOnlyList<(string, SkuClassification)>>(
            transferState.Entries["catalog"]);
        Assert.All(serialized, item => Assert.Equal(SkuClassification.Vegetarian, item.Item2));
        Assert.DoesNotContain(serialized, item => item.Item1.Contains("BEEF", StringComparison.Ordinal));
    }
}
