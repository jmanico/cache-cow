using System.Globalization;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using CacheCow.Modules.CatalogInventory.Catalog;
using CacheCow.Modules.MarketGating.Enforcement;
using CacheCow.Modules.MarketGating.Policy;
using CacheCow.Modules.MarketGating.Resolution;
using CacheCow.Modules.PricingPromotions.Pricing;
using CacheCow.SharedKernel;

namespace CacheCow.Host.Composition.Seo;

/// <summary>
/// Composes the machine-consumed SEO surfaces — per-market sitemaps, the
/// sitemap index, per-market product feeds, and Product JSON-LD — from typed
/// catalog and price data, with every SKU and content experience passing
/// through the REAL Market &amp; Gating enforcement point per generation
/// (CC-MKT-003 names sitemaps, structured data, and feeds as exclusion
/// surfaces; ARCHITECTURE.md Dependency rule 1: no market conditionals live
/// here — the policy decisions are the gating service's). The transacting
/// market comes exclusively from the server-side addressed market (the route),
/// never from client hints (CC-SEC-012), and generation fails closed: an
/// unclassifiable, gated, market-unflagged, or unpriced SKU is excluded,
/// never emitted (SECURITY.md, Logging rule 2).
///
/// Output is built exclusively by serializers (XDocument / System.Text.Json) —
/// no string-assembled markup; no XML is ever parsed here (SECURITY.md, Input
/// validation rule 9).
/// </summary>
public sealed class SeoSurfaceComposer
{
    private static readonly XNamespace SitemapNs = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private static readonly XNamespace XhtmlNs = "http://www.w3.org/1999/xhtml";

    /// <summary>
    /// First-party feed namespace. "Feeds" (CC-MKT-003) has no ratified format
    /// or consumer (issue 071, Open Questions); this minimal first-party XML
    /// vocabulary exists so the gated emission path and its tests are real,
    /// and is versioned so a ratified format can supersede it.
    /// </summary>
    private static readonly XNamespace FeedNs = "urn:cachecow:feed:products:v1";

    /// <summary>
    /// Content-page slugs of the consumer page inventory (DESIGN.md §10) that
    /// belong in a sitemap. Gated experiences carry their
    /// <see cref="ContentExperience"/> so availability is the gating service's
    /// decision (CC-MKT-005: no /cuts URL may appear in the IN sitemap);
    /// ungated pages exist in every market. Slug naming is part of the
    /// undecided URL model (see <see cref="SeoUrlScheme"/>).
    /// </summary>
    private static readonly (string Slug, ContentExperience? Experience)[] ContentPages =
    [
        (string.Empty, null), // market home
        ("menu", null),
        ("chefs", null),
        ("cows", ContentExperience.MeetOurCows),
        ("cuts", ContentExperience.MeetOurCuts),
    ];

    private readonly IMarketGatingService _gating;
    private readonly ISkuCatalog _catalog;

    public SeoSurfaceComposer(IMarketGatingService gating, ISkuCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(gating);
        ArgumentNullException.ThrowIfNull(catalog);
        _gating = gating;
        _catalog = catalog;
    }

    /// <summary>The /sitemap.xml index: the six per-market sitemaps (CC-MKT-001), nothing market-variant.</summary>
    public XDocument SitemapIndex(Uri baseUrl)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        var root = baseUrl.AbsoluteUri.TrimEnd('/');

        return new XDocument(new XElement(
            SitemapNs + "sitemapindex",
            Market.All.Select(market => new XElement(
                SitemapNs + "sitemap",
                new XElement(SitemapNs + "loc", root + SeoUrlScheme.SitemapPath(market))))));
    }

    /// <summary>
    /// One market's sitemap: gated content pages plus the market's gated,
    /// available product URLs, each entry carrying hreflang alternates for
    /// all seven launch locales (CC-I18N-004; locale is user-selectable
    /// independent of market, CC-I18N-001, so every page exists in every
    /// launch locale). The canonical loc uses the deterministic structural
    /// carrier locale — presentation only, never a gating input.
    /// </summary>
    public XDocument MarketSitemap(Uri baseUrl, Market market)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);

        var context = new TransactingContext(market, SkuGating.CarrierLocaleFor(market));
        var urlset = new XElement(
            SitemapNs + "urlset",
            new XAttribute(XNamespace.Xmlns + "xhtml", XhtmlNs));

        foreach (var (slug, experience) in ContentPages)
        {
            // Gated experiences are absent entirely — not linked, not listed
            // (CC-MKT-005 via the enforcement point; fail closed on denial).
            if (experience is { } gatedExperience
                && !_gating.EvaluateContentExperience(context, gatedExperience).IsAllowed)
            {
                continue;
            }

            urlset.Add(UrlEntry(baseUrl, market, context.Locale, slug));
        }

        foreach (var sku in GatedSkusFor(context, ResponseSurface.Sitemap))
        {
            urlset.Add(UrlEntry(
                baseUrl,
                market,
                context.Locale,
                "products/" + Uri.EscapeDataString(sku.Id.Value)));
        }

        return new XDocument(urlset);
    }

    /// <summary>
    /// One market's product feed (first-party XML, see <see cref="FeedNs"/>):
    /// the same gating discipline as the sitemap on the
    /// <see cref="ResponseSurface.Feed"/> surface, with prices emitted as
    /// integer minor units + ISO currency code from the canonical
    /// <see cref="PriceList"/> — never a binary-float representation
    /// (CC-PRC-003). An unpriced SKU has no consumer offer in that market and
    /// is excluded (CC-PRC-001; fail closed).
    /// </summary>
    public XDocument ProductFeed(Uri baseUrl, Market market, PriceList prices)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        ArgumentNullException.ThrowIfNull(prices);

        var context = new TransactingContext(market, SkuGating.CarrierLocaleFor(market));
        var root = new XElement(
            FeedNs + "products",
            new XAttribute("market", market.Code),
            new XAttribute("locale", context.Locale.Tag));

        foreach (var sku in GatedSkusFor(context, ResponseSurface.Feed))
        {
            var lookup = prices.Lookup(sku.Id, market);
            if (!lookup.IsPriced)
            {
                continue; // no price row in this market = no offer here (CC-PRC-001)
            }

            var money = lookup.Price.UnitPrice;
            var product = new XElement(
                FeedNs + "product",
                new XElement(FeedNs + "id", sku.Id.Value));

            // Exact-locale name only; localization fallback policy is an open
            // Content & Localization concern — omission, never invention.
            if (sku.Name.TryGet(context.Locale, out var name))
            {
                product.Add(new XElement(FeedNs + "name", name));
            }

            product.Add(
                new XElement(
                    FeedNs + "classification",
                    sku.Classification == ProductClassification.Vegetarian ? "vegetarian" : "non-vegetarian"),
                new XElement(FeedNs + "url", SeoUrlScheme.ProductUrl(baseUrl, market, context.Locale, sku.Id)),
                new XElement(
                    FeedNs + "price",
                    new XAttribute("minorUnits", money.MinorUnits.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute("currency", money.Currency.Code)));

            root.Add(product);
        }

        return new XDocument(root);
    }

    /// <summary>
    /// Product JSON-LD (schema.org Product/Offer) for PDP SSR embedding,
    /// generated only from typed catalog + price data already gated for the
    /// requesting market on the <see cref="ResponseSurface.StructuredData"/>
    /// surface — never for a SKU excluded from that market (CC-MKT-003;
    /// SECURITY.md HTTP boundary rule 10's transfer-state principle applied to
    /// embedded payloads). Refusals are typed and reason-free; they present as
    /// 404, indistinguishable from a nonexistent SKU (CC-MKT-004).
    /// </summary>
    public ProductStructuredData ProductJsonLd(Uri baseUrl, Market market, SkuId skuId, PriceList prices)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        ArgumentNullException.ThrowIfNull(prices);

        if (!_catalog.TryGet(skuId, out var sku)
            || !sku.IsAvailableIn(market)
            || !SkuGating.IsPermitted(_gating, _catalog, market, skuId, ResponseSurface.StructuredData))
        {
            return ProductStructuredData.Refused;
        }

        var lookup = prices.Lookup(skuId, market);
        if (!lookup.IsPriced)
        {
            return ProductStructuredData.Refused; // no offer in this market (CC-PRC-001; fail closed)
        }

        var context = new TransactingContext(market, SkuGating.CarrierLocaleFor(market));
        var url = SeoUrlScheme.ProductUrl(baseUrl, market, context.Locale, skuId);
        var money = lookup.Price.UnitPrice;

        var document = new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Product",
            ["sku"] = skuId.Value,
            ["url"] = url,
        };

        if (sku.Name.TryGet(context.Locale, out var name))
        {
            document["name"] = name; // exact-locale only; no fallback invention
        }

        document["offers"] = new JsonObject
        {
            ["@type"] = "Offer",
            ["price"] = ExactDecimalPrice(money), // exact integer-derived string, never float (CC-PRC-003)
            ["priceCurrency"] = money.Currency.Code,
            ["url"] = url,
        };

        return ProductStructuredData.Of(document.ToJsonString());
    }

    /// <summary>
    /// The market's emittable SKUs in deterministic order: flagged available
    /// in the market (CC-CAT-001 master data) AND passed through the bulk
    /// gating path for the declared surface — the availability flag is data,
    /// the exclusion decision is the enforcement point's (CC-MKT-006).
    /// </summary>
    private IEnumerable<Sku> GatedSkusFor(TransactingContext context, ResponseSurface surface)
    {
        var candidates = _catalog.All()
            .Where(sku => sku.IsAvailableIn(context.Market))
            .OrderBy(sku => sku.Id.Value, StringComparer.Ordinal);

        return _gating.FilterSkus(
            context,
            candidates,
            sku => SkuGating.Classify(sku.Classification),
            surface).Value;
    }

    private static XElement UrlEntry(Uri baseUrl, Market market, Locale canonicalLocale, string pagePath)
    {
        var entry = new XElement(
            SitemapNs + "url",
            new XElement(SitemapNs + "loc", SeoUrlScheme.PageUrl(baseUrl, market, canonicalLocale, pagePath)));

        // xhtml:link alternates per the sitemap protocol's localized-page
        // annotation, one per launch locale, BCP 47 tags from the kernel
        // Locale type — never hand-built strings (CC-I18N-004, CC-I18N-001).
        foreach (var locale in LaunchLocales.All)
        {
            entry.Add(new XElement(
                XhtmlNs + "link",
                new XAttribute("rel", "alternate"),
                new XAttribute("hreflang", locale.Tag),
                new XAttribute("href", SeoUrlScheme.PageUrl(baseUrl, market, locale, pagePath))));
        }

        return entry;
    }

    /// <summary>
    /// schema.org's price is a decimal string; derived from integer minor
    /// units with integer arithmetic only (Math.DivRem) — binary floating
    /// point never touches money, including here (CC-PRC-003).
    /// </summary>
    private static string ExactDecimalPrice(Money money)
    {
        var exponent = money.Currency.MinorUnitExponent;
        if (exponent == 0)
        {
            return money.MinorUnits.ToString(CultureInfo.InvariantCulture);
        }

        var scale = 1L;
        for (var i = 0; i < exponent; i++)
        {
            scale *= 10;
        }

        var (units, fraction) = Math.DivRem(money.MinorUnits, scale);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{units}.{fraction.ToString(CultureInfo.InvariantCulture).PadLeft(exponent, '0')}");
    }
}

/// <summary>
/// Typed outcome of structured-data generation: either the JSON-LD payload, or
/// a reason-free refusal that presents as 404 (CC-MKT-004 semantics — a gated
/// SKU is indistinguishable from a nonexistent one). Reading
/// <see cref="JsonLd"/> on a refusal throws (fail closed).
/// </summary>
public sealed class ProductStructuredData
{
    private readonly string? _jsonLd;

    private ProductStructuredData(string? jsonLd)
    {
        _jsonLd = jsonLd;
    }

    /// <summary>The single refusal instance; carries no detail by construction (SECURITY.md, Logging rule 1).</summary>
    public static ProductStructuredData Refused { get; } = new(null);

    public bool IsAvailable => _jsonLd is not null;

    public string JsonLd =>
        _jsonLd ?? throw new InvalidOperationException(
            "Structured data was refused for this market; nothing may be emitted (CC-MKT-003, fail closed).");

    public static ProductStructuredData Of(string jsonLd)
    {
        ArgumentException.ThrowIfNullOrEmpty(jsonLd);
        return new ProductStructuredData(jsonLd);
    }
}
