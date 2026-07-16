using CacheCow.SharedKernel;

namespace CacheCow.Host.Composition.Seo;

/// <summary>
/// The host's URL scheme for SEO surfaces. The URL model encoding market and
/// locale (path prefix vs. subdomain vs. query) is UNSPECIFIED by the specs
/// (issue 071, Open Questions); this file is the single documented first-party
/// choice, kept in one place so a human decision can replace it wholesale:
///
///   {baseUrl}/{market-slug}/{locale-tag}/{page-path}
///   e.g. https://host/in/en-IN/products/PANEER-01
///
/// The market slug is the lowercase launch-market code and is resolved
/// EXACT-match only — an unknown or wrong-case slug is rejected (404), never
/// coerced (CC-MKT-001 discipline; SECURITY.md, Input validation rule 1). The
/// market always comes from the request PATH — server-addressed state — never
/// from Accept-Language, geolocation, or cookies (CC-SEC-012).
/// </summary>
internal static class SeoUrlScheme
{
    /// <summary>The sitemap index (CC-MKT-001: the six per-market sitemaps); market-invariant.</summary>
    internal const string SitemapIndexPath = "/sitemap.xml";

    /// <summary>
    /// Route template for one market's sitemap. Deliberately adjacent to
    /// <see cref="SitemapPath"/>, which generates the very URLs this template
    /// must match: the emitted index entries and the served routes are one
    /// source of truth, so they cannot drift apart.
    /// </summary>
    internal const string SitemapRouteTemplate = "/sitemap-{market}.xml";

    /// <summary>Route template for one market's product feed; pairs with <see cref="FeedPath"/>.</summary>
    internal const string FeedRouteTemplate = "/feeds/products-{market}.xml";

    private static readonly Dictionary<string, Market> BySlug =
        Market.All.ToDictionary(SlugOf, market => market, StringComparer.Ordinal);

    /// <summary>Lowercase launch-market code, e.g. "in" for IN.</summary>
    internal static string SlugOf(Market market)
    {
        var code = market.Code;
        return string.Create(code.Length, code, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                span[i] = char.ToLowerInvariant(source[i]);
            }
        });
    }

    /// <summary>
    /// Exact-match slug resolution: only the six lowercase launch-market codes
    /// resolve; everything else (unknown market, wrong case) is rejected.
    /// </summary>
    internal static bool TryResolveMarketSlug(string? slug, out Market market)
    {
        if (slug is not null && BySlug.TryGetValue(slug, out market))
        {
            return true;
        }

        market = default;
        return false;
    }

    /// <summary>Absolute page URL for one market + locale; empty path is the market home.</summary>
    internal static string PageUrl(Uri baseUrl, Market market, Locale locale, string pagePath)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        var root = baseUrl.AbsoluteUri.TrimEnd('/');
        return $"{root}/{SlugOf(market)}/{locale.Tag}/{pagePath}";
    }

    /// <summary>Absolute product URL; the SKU id is percent-encoded, never emitted raw.</summary>
    internal static string ProductUrl(Uri baseUrl, Market market, Locale locale, SkuId sku) =>
        PageUrl(baseUrl, market, locale, "products/" + Uri.EscapeDataString(sku.Value));

    /// <summary>Host-relative path of one market's sitemap, e.g. "/sitemap-in.xml".</summary>
    internal static string SitemapPath(Market market) => $"/sitemap-{SlugOf(market)}.xml";

    /// <summary>Host-relative path of one market's product feed, e.g. "/feeds/products-in.xml".</summary>
    internal static string FeedPath(Market market) => $"/feeds/products-{SlugOf(market)}.xml";
}
