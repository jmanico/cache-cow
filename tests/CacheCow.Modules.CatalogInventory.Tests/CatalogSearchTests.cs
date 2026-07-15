using CacheCow.Modules.CatalogInventory.Catalog;
using CacheCow.Modules.CatalogInventory.Search;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.CatalogInventory.Tests;

/// <summary>
/// Issue 031: catalog search operates per market and per locale (CC-CAT-005)
/// with a single-toggle vegetarian filter in all markets (CC-CAT-006).
/// Gating composition: the caller-supplied classification predicate stands in
/// for the Market &amp; Gating Policy enforcement point (ARCHITECTURE.md,
/// Dependency rule 1) — full parity is asserted by composition tests in the
/// gating matrix (issue 027, CC-QA-003).
/// </summary>
public sealed class CatalogSearchTests
{
    private static readonly Func<ProductClassification, bool> AllowAll = _ => true;

    /// <summary>What the gating enforcement point supplies for IN (CC-MKT-003).</summary>
    private static readonly Func<ProductClassification, bool> VegOnlyGate =
        classification => classification == ProductClassification.Vegetarian;

    private static InMemoryCatalogSearchService Service(params Sku[] skus)
    {
        var catalog = new InMemorySkuCatalog();
        foreach (var sku in skus)
        {
            catalog.Add(sku);
        }

        return new InMemoryCatalogSearchService(catalog);
    }

    private static Sku PaneerSku() => new SkuBuilder()
        .WithId("VEG-PANEER-01")
        .WithClassification(ProductClassification.Vegetarian)
        .WithName(TestLocales.EnUs, "Smoked Paneer")
        .WithName(TestLocales.HiIn, "स्मोक्ड पनीर")
        .WithName(TestLocales.JaJp, "スモークパニール")
        .WithName(TestLocales.EsEs, "Paneer ahumado")
        .WithName(TestLocales.EsMx, "Paneer ahumado")
        .WithName(TestLocales.DeDe, "Geräucherter Paneer")
        .WithName(TestLocales.EnIn, "Smoked Paneer")
        .Build();

    private static Sku BrisketSku() => new SkuBuilder()
        .WithId("BEEF-BRISKET-01")
        .WithClassification(ProductClassification.NonVegetarian)
        .WithName(TestLocales.EnUs, "Smoked Beef Brisket")
        .WithName(TestLocales.JaJp, "スモークビーフブリスケット")
        .WithName(TestLocales.DeDe, "Geräucherte Rinderbrust")
        .WithName(TestLocales.EsEs, "Falda de res ahumada")
        .WithName(TestLocales.EsMx, "Falda de res ahumada")
        .Build();

    [Fact]
    [Requirement("CC-CAT-005")]
    public void A_japanese_query_matches_the_japanese_product_name()
    {
        var service = Service(PaneerSku(), BrisketSku());
        var query = CatalogSearchQuery.Create(Market.JP, TestLocales.JaJp, "ブリスケット");

        var results = service.Search(query, AllowAll);

        Assert.Single(results);
        Assert.Equal(SkuId.Parse("BEEF-BRISKET-01"), results[0].Id);
    }

    [Fact]
    [Requirement("CC-CAT-005")]
    public void A_hiragana_query_matches_a_katakana_name_via_kana_insensitive_matching()
    {
        // Interim in-memory normalization (kana type + width insensitivity).
        // The mechanism giving PostgreSQL FTS equivalent ja-JP quality is an
        // open question on issue 031 — flagged, not resolved here.
        var service = Service(BrisketSku());
        var query = CatalogSearchQuery.Create(Market.JP, TestLocales.JaJp, "ぶりすけっと");

        var results = service.Search(query, AllowAll);

        Assert.Single(results);
    }

    [Fact]
    [Requirement("CC-CAT-005")]
    public void A_devanagari_query_matches_the_hindi_product_name()
    {
        var service = Service(PaneerSku(), BrisketSku());
        var query = CatalogSearchQuery.Create(Market.IN, TestLocales.HiIn, "पनीर");

        var results = service.Search(query, VegOnlyGate);

        Assert.Single(results);
        Assert.Equal(SkuId.Parse("VEG-PANEER-01"), results[0].Id);
    }

    [Fact]
    [Requirement("CC-CAT-005")]
    public void Matching_uses_the_name_in_the_query_locale_only()
    {
        var service = Service(BrisketSku());

        // The German name matches under de-DE...
        var german = CatalogSearchQuery.Create(Market.DE, TestLocales.DeDe, "Rinderbrust");
        Assert.Single(service.Search(german, AllowAll));

        // ...but not when the query locale is en-US: locale drives which
        // localized name is searched (CC-CAT-005).
        var english = CatalogSearchQuery.Create(Market.DE, TestLocales.EnUs, "Rinderbrust");
        Assert.Empty(service.Search(english, AllowAll));
    }

    [Fact]
    [Requirement("CC-CAT-005")]
    public void A_sku_without_a_name_in_the_query_locale_does_not_match_a_text_query()
    {
        // BrisketSku has no hi-IN name: exact-locale matching fails closed.
        var service = Service(BrisketSku());
        var query = CatalogSearchQuery.Create(Market.US, TestLocales.HiIn, "ब्रिस्केट");

        Assert.Empty(service.Search(query, AllowAll));
    }

    [Fact]
    [Requirement("CC-CAT-005")]
    public void Case_insensitive_matching_applies_to_latin_locales()
    {
        var service = Service(BrisketSku());
        var query = CatalogSearchQuery.Create(Market.US, TestLocales.EnUs, "smoked beef");

        Assert.Single(service.Search(query, AllowAll));
    }

    [Fact]
    [Requirement("CC-CAT-005")]
    public void Results_are_scoped_to_the_transacting_market()
    {
        var jpOnly = new SkuBuilder()
            .WithId("JP-ONLY-01")
            .WithName(TestLocales.EnUs, "Gift Grade Wagyu Brisket")
            .WithClassification(ProductClassification.NonVegetarian)
            .WithAvailableMarkets(Market.JP)
            .Build();
        var service = Service(jpOnly);

        // Available solely in JP: surfaces there, never in US (issue 031 AC-04).
        var jpQuery = CatalogSearchQuery.Create(Market.JP, TestLocales.EnUs, "Wagyu");
        Assert.Single(service.Search(jpQuery, AllowAll));

        var usQuery = CatalogSearchQuery.Create(Market.US, TestLocales.EnUs, "Wagyu");
        Assert.Empty(service.Search(usQuery, AllowAll));
    }

    [Theory]
    [Requirement("CC-CAT-006")]
    [Requirement("CC-MKT-007")]
    [InlineData("US")]
    [InlineData("ES")]
    [InlineData("MX")]
    [InlineData("DE")]
    [InlineData("JP")]
    [InlineData("IN")]
    public void The_single_toggle_vegetarian_filter_returns_veg_skus_only_in_every_market(string marketCode)
    {
        var market = Market.Parse(marketCode);
        var service = Service(PaneerSku(), BrisketSku());

        // The gate reflects what the enforcement point supplies per market;
        // the veg toggle composes on top of it (CC-CAT-006).
        var gate = market == Market.IN ? VegOnlyGate : AllowAll;

        var query = CatalogSearchQuery.Create(market, TestLocales.EnUs, string.Empty, vegetarianOnly: true);
        var results = service.Search(query, gate);

        Assert.NotEmpty(results);
        Assert.All(results, sku => Assert.Equal(ProductClassification.Vegetarian, sku.Classification));
    }

    [Fact]
    [Requirement("CC-CAT-005")]
    [Requirement("CC-MKT-003")]
    public void The_gating_predicate_excludes_non_veg_even_on_an_exact_name_match()
    {
        var service = Service(PaneerSku(), BrisketSku());

        // Exact non-veg product name, IN gate in force: zero non-veg results
        // (issue 031 AC-01; CC-MKT-003 parity is asserted end-to-end by the
        // gating matrix, issue 027).
        var query = CatalogSearchQuery.Create(Market.IN, TestLocales.EnUs, "Smoked Beef Brisket");
        var results = service.Search(query, VegOnlyGate);

        Assert.Empty(results);
    }

    [Fact]
    [Requirement("CC-CAT-005")]
    [Requirement("CC-MKT-003")]
    public void A_failing_gating_predicate_fails_closed_and_never_returns_ungated_results()
    {
        var service = Service(PaneerSku(), BrisketSku());
        var query = CatalogSearchQuery.Create(Market.IN, TestLocales.EnUs, string.Empty);

        // If the enforcement point cannot be consulted the search errors —
        // never ungated results (issue 031, Failure Behavior).
        Assert.Throws<InvalidOperationException>(() => service.Search(
            query, _ => throw new InvalidOperationException("Gating unavailable.")));
    }

    [Fact]
    [Requirement("CC-CAT-005")]
    public void An_empty_query_lists_all_permitted_skus_in_deterministic_order()
    {
        var service = Service(PaneerSku(), BrisketSku());
        var query = CatalogSearchQuery.Create(Market.US, TestLocales.EnUs, string.Empty);

        var results = service.Search(query, AllowAll);

        Assert.Equal(
            [SkuId.Parse("BEEF-BRISKET-01"), SkuId.Parse("VEG-PANEER-01")],
            results.Select(sku => sku.Id).ToArray());
    }

    [Fact]
    [Requirement("CC-CAT-005")]
    public void An_oversized_query_is_rejected_not_truncated()
    {
        var oversized = new string('a', CatalogSearchQuery.MaxTextLength + 1);

        Assert.Throws<ArgumentException>(() =>
            CatalogSearchQuery.Create(Market.US, TestLocales.EnUs, oversized));

        // The boundary itself is accepted.
        var atLimit = new string('a', CatalogSearchQuery.MaxTextLength);
        Assert.Equal(atLimit, CatalogSearchQuery.Create(Market.US, TestLocales.EnUs, atLimit).Text);
    }

    [Fact]
    [Requirement("CC-CAT-005")]
    public void A_query_requires_server_side_market_and_locale_state()
    {
        // Uninitialized market/locale cannot construct a query: scoping keys
        // off server-resolved transacting state, never client hints
        // (CC-SEC-012; SECURITY.md, Authentication rule 10).
        Assert.Throws<ArgumentException>(() =>
            CatalogSearchQuery.Create(default, TestLocales.EnUs, "brisket"));
        Assert.Throws<ArgumentException>(() =>
            CatalogSearchQuery.Create(Market.US, default, "brisket"));
        Assert.Throws<ArgumentNullException>(() =>
            CatalogSearchQuery.Create(Market.US, TestLocales.EnUs, null!));
    }

    [Fact]
    [Requirement("CC-CAT-005")]
    public void Search_text_with_query_metacharacters_is_treated_as_literal_text()
    {
        // The in-memory matcher treats input as plain text; the FTS adapter
        // must preserve this via parameterized plainto_tsquery/websearch_to_tsquery
        // (issue 031 AC-06; SECURITY.md, Input validation rule 4).
        var service = Service(PaneerSku());
        var query = CatalogSearchQuery.Create(
            Market.US, TestLocales.EnUs, "'; DROP TABLE skus; -- & | ! ( ) : *");

        Assert.Empty(service.Search(query, AllowAll));
    }
}
