using System.Net;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using CacheCow.Host.Composition.Seo;
using CacheCow.Modules.CatalogInventory.Catalog;
using CacheCow.Modules.PricingPromotions.Pricing;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CacheCow.Host.Tests.Composition;

/// <summary>
/// Issue 071: the public SEO surfaces — per-market sitemaps, the sitemap index,
/// per-market product feeds, and Product JSON-LD — composed through the REAL
/// registered MarketGating service in the real host pipeline (nothing is
/// faked here; ARCHITECTURE.md, Dependency rule 1).
///
/// THE LOAD-BEARING FIXTURE DETAIL: the non-veg SKU is deliberately flagged
/// AVAILABLE IN EVERY MARKET, INCLUDING IN. Master-data availability
/// (CC-CAT-001) would otherwise drop it from the IN surfaces on its own and
/// the exclusion tests would prove nothing. Mis-seeding it this way means the
/// gating enforcement point is the ONLY thing standing between a non-veg SKU
/// and an IN sitemap/feed/JSON-LD payload — which is exactly the claim
/// CC-MKT-003 makes (same trick as the CC-API-007 parity suite).
/// </summary>
public sealed class SeoSurfaceCompositionTests : IDisposable
{
    private const string NonVegSku = "BRISKET-01";
    private const string VegSku = "PANEER-01";

    /// <summary>TEST-ONLY host name: production hostnames are undecided (ARCHITECTURE.md, "Known unknowns").</summary>
    private const string BaseUrl = "https://seo.test";
    private const int MaxAgeSeconds = 600;

    /// <summary>Matches the net weight <see cref="B2BFixtures.CatalogSku"/> builds (CC-CAT-001).</summary>
    private const long FixtureNetWeightGrams = 500;

    private static readonly XNamespace SitemapNs = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private static readonly XNamespace XhtmlNs = "http://www.w3.org/1999/xhtml";
    private static readonly XNamespace FeedNs = "urn:cachecow:feed:products:v1";

    private readonly WebApplicationFactory<Program> _factory;

    public SeoSurfaceCompositionTests()
    {
        _factory = CreateHost(new Dictionary<string, string?>
        {
            ["Seo:BaseUrl"] = BaseUrl,
            ["Seo:PublicMaxAgeSeconds"] = MaxAgeSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
        });
    }

    public void Dispose() => _factory.Dispose();

    private static WebApplicationFactory<Program> CreateHost(IDictionary<string, string?> config)
    {
        var factory = TestHostBuilder.Create(config, configureServices: services =>
            services.AddSingleton(new PriceList(
            [
                new MarketPrice(SkuId.Parse(VegSku), Market.US, Money.FromMinorUnits(3_000, Currency.Usd)),
                new MarketPrice(SkuId.Parse(VegSku), Market.IN, Money.FromMinorUnits(120_000, Currency.Inr)),
                // DE prices carry the net weight the Preisangabenverordnung
                // unit-price-per-kg derivation needs (CC-PRC-002); it matches
                // the fixture SKU's 500 g net weight.
                new MarketPrice(SkuId.Parse(VegSku), Market.DE, Money.FromMinorUnits(2_800, Currency.Eur), FixtureNetWeightGrams),
                new MarketPrice(SkuId.Parse(VegSku), Market.JP, Money.FromMinorUnits(4_500, Currency.Jpy)),

                // MX deliberately prices ONLY the veg SKU: the non-veg SKU is
                // available and gating-PERMITTED in MX, so its absence from the
                // MX feed can only be the missing price row, never gating.
                new MarketPrice(SkuId.Parse(VegSku), Market.MX, Money.FromMinorUnits(60_000, Currency.Mxn)),
                new MarketPrice(SkuId.Parse(NonVegSku), Market.US, Money.FromMinorUnits(5_000, Currency.Usd)),
                new MarketPrice(SkuId.Parse(NonVegSku), Market.DE, Money.FromMinorUnits(4_900, Currency.Eur), FixtureNetWeightGrams),
                new MarketPrice(SkuId.Parse(NonVegSku), Market.JP, Money.FromMinorUnits(14_900, Currency.Jpy)),

                // Mis-seeded on purpose, like the availability flag below: a
                // consumer price for a non-veg SKU in IN. Only gating stops it.
                new MarketPrice(SkuId.Parse(NonVegSku), Market.IN, Money.FromMinorUnits(200_000, Currency.Inr)),
            ])));

        B2BFixtures.SeedCatalog(
            factory,
            B2BFixtures.CatalogSku(NonVegSku, ProductClassification.NonVegetarian, Market.All.ToArray()),
            B2BFixtures.CatalogSku(VegSku, ProductClassification.Vegetarian, Market.All.ToArray()));

        return factory;
    }

    /// <summary>
    /// Parses XML the way SECURITY.md, Input validation rule 9 requires — DTD
    /// processing prohibited, no resolver — so the suite that asserts the
    /// output never becomes the XXE hole the product code avoids.
    /// </summary>
    private static XDocument ParseXml(string xml)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(new StringReader(xml), settings);
        return XDocument.Load(reader);
    }

    private async Task<(HttpResponseMessage Response, string Body)> GetAsync(string path, HttpClient? client = null)
    {
        var owned = client is null ? _factory.CreateHttpsClient() : null;
        try
        {
            var response = await (client ?? owned!).GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
            return (response, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            owned?.Dispose();
        }
    }

    private async Task<XDocument> GetXmlAsync(string path)
    {
        var (response, body) = await GetAsync(path);
        using (response)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return ParseXml(body);
        }
    }

    private static List<string> Locations(XDocument sitemap) =>
        sitemap.Root!.Elements(SitemapNs + "url")
            .Select(url => url.Element(SitemapNs + "loc")!.Value)
            .ToList();

    // ---- Sitemaps: the marquee CC-MKT-003 exclusion ------------------------

    [Fact]
    [Requirement("CC-MKT-003")]
    [Requirement("CC-QA-003")]
    public async Task IN_sitemap_lists_the_veg_sku_and_never_the_nonveg_sku()
    {
        var (response, body) = await GetAsync("/sitemap-in.xml");
        using var _ = response;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var locations = Locations(ParseXml(body));
        Assert.Contains(locations, loc => loc.EndsWith("/products/" + VegSku, StringComparison.Ordinal));
        Assert.DoesNotContain(locations, loc => loc.Contains(NonVegSku, StringComparison.OrdinalIgnoreCase));

        // Not in a loc, not in an hreflang alternate, not anywhere in the
        // document — the SKU is absent from the response, not merely unlinked.
        Assert.DoesNotContain(NonVegSku, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Requirement("CC-MKT-003")]
    [Requirement("CC-MKT-007")]
    [Requirement("CC-QA-003")]
    public async Task US_sitemap_lists_both_skus_proving_the_IN_exclusion_is_market_gating()
    {
        var locations = Locations(await GetXmlAsync("/sitemap-us.xml"));

        Assert.Contains(locations, loc => loc.EndsWith("/products/" + VegSku, StringComparison.Ordinal));
        Assert.Contains(locations, loc => loc.EndsWith("/products/" + NonVegSku, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("es")]
    [InlineData("mx")]
    [InlineData("de")]
    [InlineData("jp")]
    [Requirement("CC-MKT-007")]
    [Requirement("CC-QA-003")]
    public async Task Full_catalog_markets_all_carry_the_nonveg_sku(string marketSlug)
    {
        var locations = Locations(await GetXmlAsync($"/sitemap-{marketSlug}.xml"));

        Assert.Contains(locations, loc => loc.EndsWith("/products/" + NonVegSku, StringComparison.Ordinal));
        Assert.Contains(locations, loc => loc.EndsWith("/products/" + VegSku, StringComparison.Ordinal));
    }

    // ---- Content-page gating (CC-MKT-005) ---------------------------------

    [Fact]
    [Requirement("CC-MKT-005")]
    public async Task Cuts_is_absent_from_the_IN_sitemap_and_present_in_DE()
    {
        var inLocations = Locations(await GetXmlAsync("/sitemap-in.xml"));
        var deLocations = Locations(await GetXmlAsync("/sitemap-de.xml"));

        // "Meet our Cuts" is unreachable in IN: not listed, not linked.
        Assert.DoesNotContain(inLocations, loc => loc.EndsWith("/cuts", StringComparison.Ordinal));
        Assert.Contains(deLocations, loc => loc.EndsWith("/cuts", StringComparison.Ordinal));

        // The herd page is present in IN — proving the absence above is the
        // gating decision, not an empty or broken content-page set.
        Assert.Contains(inLocations, loc => loc.EndsWith("/cows", StringComparison.Ordinal));
    }

    // ---- hreflang alternates (CC-I18N-004) --------------------------------

    [Fact]
    [Requirement("CC-I18N-004")]
    [Requirement("CC-I18N-001")]
    public async Task Every_sitemap_entry_declares_hreflang_alternates_for_all_seven_launch_locales()
    {
        var sitemap = await GetXmlAsync("/sitemap-de.xml");

        var expected = new[] { "en-US", "es-ES", "es-MX", "de-DE", "ja-JP", "en-IN", "hi-IN" };

        foreach (var url in sitemap.Root!.Elements(SitemapNs + "url"))
        {
            var alternates = url.Elements(XhtmlNs + "link").ToList();

            Assert.All(alternates, link => Assert.Equal("alternate", link.Attribute("rel")!.Value));
            Assert.Equal(
                expected.OrderBy(tag => tag, StringComparer.Ordinal),
                alternates.Select(link => link.Attribute("hreflang")!.Value).OrderBy(tag => tag, StringComparer.Ordinal));
        }
    }

    [Fact]
    [Requirement("CC-I18N-004")]
    public async Task Hreflang_alternate_hrefs_address_the_same_page_in_each_locale()
    {
        var sitemap = await GetXmlAsync("/sitemap-de.xml");

        var productEntry = sitemap.Root!.Elements(SitemapNs + "url")
            .Single(url => url.Element(SitemapNs + "loc")!.Value.EndsWith("/products/" + VegSku, StringComparison.Ordinal));

        var hrefByLocale = productEntry.Elements(XhtmlNs + "link")
            .ToDictionary(link => link.Attribute("hreflang")!.Value, link => link.Attribute("href")!.Value, StringComparer.Ordinal);

        // Same market, same page, one URL per locale (locale is selectable
        // independently of market, CC-I18N-001/CC-MKT-002).
        Assert.Equal($"{BaseUrl}/de/ja-JP/products/{VegSku}", hrefByLocale["ja-JP"]);
        Assert.Equal($"{BaseUrl}/de/de-DE/products/{VegSku}", hrefByLocale["de-DE"]);
    }

    // ---- Sitemap index ----------------------------------------------------

    [Fact]
    [Requirement("CC-MKT-001")]
    public async Task Sitemap_index_lists_one_sitemap_per_launch_market()
    {
        var index = await GetXmlAsync("/sitemap.xml");

        var locations = index.Root!.Elements(SitemapNs + "sitemap")
            .Select(entry => entry.Element(SitemapNs + "loc")!.Value)
            .ToList();

        Assert.Equal(
            Market.All.Select(market => $"{BaseUrl}/sitemap-{market.Code.ToLowerInvariant()}.xml")
                .OrderBy(loc => loc, StringComparer.Ordinal),
            locations.OrderBy(loc => loc, StringComparer.Ordinal));
    }

    [Fact]
    [Requirement("CC-MKT-001")]
    public async Task Every_market_sitemap_the_index_advertises_actually_resolves()
    {
        var index = await GetXmlAsync("/sitemap.xml");

        foreach (var entry in index.Root!.Elements(SitemapNs + "sitemap"))
        {
            var path = new Uri(entry.Element(SitemapNs + "loc")!.Value).AbsolutePath;
            var (response, _) = await GetAsync(path);
            using var _2 = response;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    // ---- Unknown market: 404 ----------------------------------------------

    [Theory]
    [InlineData("/sitemap-xx.xml")]
    [InlineData("/sitemap-fr.xml")]
    [InlineData("/sitemap-IN.xml")] // exact-match only: no case coercion
    [InlineData("/feeds/products-xx.xml")]
    [Requirement("CC-MKT-001")]
    [Requirement("CC-MKT-004")]
    public async Task Unknown_market_slug_is_404(string path)
    {
        var (response, _) = await GetAsync(path);
        using var _2 = response;

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Feeds: gating parity with the sitemap ----------------------------

    [Fact]
    [Requirement("CC-MKT-003")]
    [Requirement("CC-QA-003")]
    public async Task IN_feed_carries_the_veg_sku_and_never_the_nonveg_sku()
    {
        var (response, body) = await GetAsync("/feeds/products-in.xml");
        using var _ = response;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var ids = ParseXml(body).Root!.Elements(FeedNs + "product")
            .Select(product => product.Element(FeedNs + "id")!.Value)
            .ToList();

        Assert.Contains(VegSku, ids, StringComparer.Ordinal);
        Assert.DoesNotContain(NonVegSku, ids, StringComparer.Ordinal);
        Assert.DoesNotContain(NonVegSku, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Requirement("CC-MKT-003")]
    [Requirement("CC-MKT-007")]
    public async Task US_feed_carries_both_skus_proving_the_IN_feed_exclusion_is_market_gating()
    {
        var (response, body) = await GetAsync("/feeds/products-us.xml");
        using var _ = response;

        var ids = ParseXml(body).Root!.Elements(FeedNs + "product")
            .Select(product => product.Element(FeedNs + "id")!.Value)
            .ToList();

        Assert.Contains(VegSku, ids, StringComparer.Ordinal);
        Assert.Contains(NonVegSku, ids, StringComparer.Ordinal);
    }

    [Fact]
    [Requirement("CC-PRC-003")]
    [Requirement("CC-PRC-001")]
    public async Task Feed_prices_are_integer_minor_units_with_a_currency_and_never_a_float()
    {
        var feed = await GetXmlAsync("/feeds/products-us.xml");

        var price = feed.Root!.Elements(FeedNs + "product")
            .Single(product => product.Element(FeedNs + "id")!.Value == NonVegSku)
            .Element(FeedNs + "price")!;

        Assert.Equal("5000", price.Attribute("minorUnits")!.Value);
        Assert.Equal("USD", price.Attribute("currency")!.Value);

        // No decimal separator anywhere in the money representation: the wire
        // format is integer minor units, so no float can round-trip through it.
        Assert.DoesNotContain(".", price.Attribute("minorUnits")!.Value, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-PRC-001")]
    public async Task Feed_omits_a_sku_with_no_price_row_in_that_market()
    {
        var feed = await GetXmlAsync("/feeds/products-mx.xml");

        var ids = feed.Root!.Elements(FeedNs + "product")
            .Select(product => product.Element(FeedNs + "id")!.Value)
            .ToList();

        // The non-veg SKU is available in MX and NOT gated there — the MX
        // sitemap lists it — but it has no MX price row, so it has no consumer
        // offer to publish: omitted, never cross-priced from another market
        // (CC-PRC-001 forbids runtime FX / cross-market fallback).
        Assert.Equal([VegSku], ids);

        var sitemapLocations = Locations(await GetXmlAsync("/sitemap-mx.xml"));
        Assert.Contains(sitemapLocations, loc => loc.EndsWith("/products/" + NonVegSku, StringComparison.Ordinal));
    }

    // ---- Product JSON-LD (CC-MKT-003 on the structured-data surface) -------

    [Fact]
    [Requirement("CC-MKT-003")]
    [Requirement("CC-QA-003")]
    public void JsonLd_is_refused_for_a_nonveg_sku_in_IN_and_produced_in_US()
    {
        var composer = _factory.Services.GetRequiredService<SeoSurfaceComposer>();
        var prices = _factory.Services.GetRequiredService<PriceList>();
        var baseUrl = new Uri(BaseUrl);

        var gated = composer.ProductJsonLd(baseUrl, Market.IN, SkuId.Parse(NonVegSku), prices);
        var allowed = composer.ProductJsonLd(baseUrl, Market.US, SkuId.Parse(NonVegSku), prices);

        Assert.False(gated.IsAvailable);
        Assert.True(allowed.IsAvailable);

        // The refusal carries no payload to leak, and reading through it is a
        // fail-closed throw rather than a default or empty document.
        var refusal = Assert.Throws<InvalidOperationException>(() => gated.JsonLd);
        Assert.DoesNotContain(NonVegSku, refusal.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Requirement("CC-MKT-003")]
    public void JsonLd_is_produced_for_a_veg_sku_in_IN()
    {
        var composer = _factory.Services.GetRequiredService<SeoSurfaceComposer>();
        var prices = _factory.Services.GetRequiredService<PriceList>();

        var structuredData = composer.ProductJsonLd(new Uri(BaseUrl), Market.IN, SkuId.Parse(VegSku), prices);

        Assert.True(structuredData.IsAvailable);

        using var document = JsonDocument.Parse(structuredData.JsonLd);
        var root = document.RootElement;

        Assert.Equal("Product", root.GetProperty("@type").GetString());
        Assert.Equal(VegSku, root.GetProperty("sku").GetString());
        Assert.Equal("INR", root.GetProperty("offers").GetProperty("priceCurrency").GetString());
        Assert.Equal($"{BaseUrl}/in/en-IN/products/{VegSku}", root.GetProperty("url").GetString());
    }

    [Fact]
    [Requirement("CC-PRC-003")]
    public void JsonLd_price_is_derived_exactly_from_integer_minor_units()
    {
        var composer = _factory.Services.GetRequiredService<SeoSurfaceComposer>();
        var prices = _factory.Services.GetRequiredService<PriceList>();
        var baseUrl = new Uri(BaseUrl);

        var usd = composer.ProductJsonLd(baseUrl, Market.US, SkuId.Parse(NonVegSku), prices);
        var jpy = composer.ProductJsonLd(baseUrl, Market.JP, SkuId.Parse(NonVegSku), prices);

        using var usdDocument = JsonDocument.Parse(usd.JsonLd);
        using var jpyDocument = JsonDocument.Parse(jpy.JsonLd);

        // Two-decimal currency: 5000 minor units renders exactly "50.00".
        Assert.Equal("50.00", usdDocument.RootElement.GetProperty("offers").GetProperty("price").GetString());

        // JPY is zero-decimal: no fabricated decimal point (CC-QA-004 context).
        Assert.Equal("14900", jpyDocument.RootElement.GetProperty("offers").GetProperty("price").GetString());
    }

    [Fact]
    [Requirement("CC-MKT-003")]
    public void JsonLd_is_refused_for_an_unknown_sku()
    {
        var composer = _factory.Services.GetRequiredService<SeoSurfaceComposer>();
        var prices = _factory.Services.GetRequiredService<PriceList>();

        var structuredData = composer.ProductJsonLd(new Uri(BaseUrl), Market.US, SkuId.Parse("NO-SUCH-SKU"), prices);

        // Indistinguishable from the gated case: both are a bare refusal
        // (CC-MKT-004 semantics).
        Assert.False(structuredData.IsAvailable);
    }

    // ---- Cache safety (CC-MKT-009 / CC-SEC-013) ---------------------------

    [Fact]
    [Requirement("CC-SEC-013")]
    [Requirement("CC-MKT-009")]
    public async Task Market_sitemap_is_publicly_cacheable_and_sets_no_cookie()
    {
        var (response, _) = await GetAsync("/sitemap-in.xml");
        using var _2 = response;

        Assert.Equal($"public, max-age={MaxAgeSeconds}", response.Headers.CacheControl!.ToString());

        // Nothing per-user may enter these surfaces: no session, no cookie.
        Assert.False(response.Headers.Contains("Set-Cookie"));

        // Varying on a client hint is the cross-market cache-reuse hole
        // CC-MKT-009 closes; the market is in the path, so nothing varies.
        Assert.DoesNotContain(
            "Accept-Language",
            string.Join(",", response.Headers.Vary),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Requirement("CC-SEC-012")]
    [Requirement("CC-SEC-013")]
    public async Task Client_hints_cannot_change_which_market_variant_is_served()
    {
        using var forging = _factory.CreateHttpsClient();

        // Every client-supplied signal that could plausibly be mistaken for a
        // market/locale input, all pointed at a full-catalog market.
        forging.DefaultRequestHeaders.Add("Accept-Language", "de-DE,de;q=0.9");
        forging.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.7");
        forging.DefaultRequestHeaders.Add("CF-IPCountry", "DE");
        forging.DefaultRequestHeaders.Add("Cookie", "market=DE; locale=de-DE");

        var (forged, forgedBody) = await GetAsync("/sitemap-in.xml", forging);
        var (plain, plainBody) = await GetAsync("/sitemap-in.xml");
        using var _ = forged;
        using var _2 = plain;

        // Byte-identical: the path decides the market, and nothing else can.
        Assert.Equal(plainBody, forgedBody);
        Assert.DoesNotContain(NonVegSku, forgedBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Requirement("CC-MKT-009")]
    public async Task Two_markets_never_produce_the_same_document()
    {
        var (inResponse, inBody) = await GetAsync("/sitemap-in.xml");
        var (usResponse, usBody) = await GetAsync("/sitemap-us.xml");
        using var _ = inResponse;
        using var _2 = usResponse;

        // Distinct bodies at distinct URLs: a cache keyed on the URL cannot
        // serve one market's sitemap into another.
        Assert.NotEqual(inBody, usBody);
    }

    [Fact]
    [Requirement("CC-MKT-009")]
    [Requirement("CC-SEC-013")]
    public async Task Sitemap_index_is_no_store_because_it_has_no_market_to_key_on()
    {
        // Deliberate, not an oversight: the index carries no market-variant
        // content and therefore no TransactingContext, and CacheDirective
        // cannot express "cacheable without a market key" by construction
        // (CC-MKT-009). Rather than invent an unkeyed cacheable class — a
        // policy decision belonging to the MarketGating module, not to this
        // endpoint — the index is served no-store even with a lifetime set.
        var (response, _) = await GetAsync("/sitemap.xml");
        using var _2 = response;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
    }

    [Fact]
    [Requirement("CC-SEC-013")]
    public async Task Unset_cache_lifetime_serves_no_store_rather_than_inventing_one()
    {
        using var factory = CreateHost(new Dictionary<string, string?> { ["Seo:BaseUrl"] = BaseUrl });
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(new Uri("/sitemap-in.xml", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
    }

    // ---- Required configuration fails closed ------------------------------

    [Theory]
    [InlineData(null)]                       // unconfigured: no production hostname is invented
    [InlineData("")]
    [InlineData("http://seo.test")]          // not https (SECURITY.md, Input validation rule 6)
    [InlineData("seo.test")]                 // not absolute
    [InlineData("https://seo.test?x=1")]     // query
    [InlineData("https://user@seo.test")]    // user info
    [Requirement("CC-MKT-003")]
    public async Task Unconfigured_or_invalid_base_url_fails_closed_with_problem_details(string? baseUrl)
    {
        var config = new Dictionary<string, string?> { ["Seo:PublicMaxAgeSeconds"] = "600" };
        if (baseUrl is not null)
        {
            config["Seo:BaseUrl"] = baseUrl;
        }

        using var factory = CreateHost(config);
        using var client = factory.CreateHttpsClient();

        foreach (var path in new[] { "/sitemap.xml", "/sitemap-in.xml", "/feeds/products-in.xml" })
        {
            using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            // No URL is emitted on a missing or malformed base (issue 071
            // Failure Behavior: ungated/misaddressed output is never emitted).
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal("no-store", response.Headers.CacheControl!.ToString());

            // RFC 9457 shape, and no internal detail about which setting is
            // missing (SECURITY.md, Logging rule 1).
            using var problem = JsonDocument.Parse(body);
            Assert.Equal(503, problem.RootElement.GetProperty("status").GetInt32());
            Assert.DoesNotContain("BaseUrl", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Seo:", body, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Requirement("CC-PRC-001")]
    public async Task Feed_fails_closed_when_no_canonical_price_list_is_registered()
    {
        // The shipped host registers no PriceList (its durable store is a
        // later issue); the feed must refuse rather than publish an empty
        // catalog to merchants.
        using var factory = TestHostBuilder.Create(new Dictionary<string, string?>
        {
            ["Seo:BaseUrl"] = BaseUrl,
        });
        using var client = factory.CreateHttpsClient();

        using var feed = await client.GetAsync(new Uri("/feeds/products-in.xml", UriKind.Relative), TestContext.Current.CancellationToken);
        using var sitemap = await client.GetAsync(new Uri("/sitemap-in.xml", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, feed.StatusCode);

        // The sitemap does not depend on prices and stays available.
        Assert.Equal(HttpStatusCode.OK, sitemap.StatusCode);
    }

    // ---- Anonymous reachability -------------------------------------------

    [Fact]
    [Requirement("CC-MKT-003")]
    public async Task Seo_surfaces_are_reachable_anonymously_and_serve_xml()
    {
        // Crawlers are unauthenticated: these routes must opt out of the
        // deny-by-default fallback policy, and must not 401.
        foreach (var path in new[] { "/sitemap.xml", "/sitemap-in.xml", "/feeds/products-in.xml" })
        {
            var (response, _) = await GetAsync(path);
            using var _2 = response;

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/xml", response.Content.Headers.ContentType!.MediaType);
        }
    }
}
