using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using CacheCow.Modules.MarketGating.Caching;
using CacheCow.Modules.MarketGating.Resolution;
using CacheCow.Modules.PricingPromotions.Pricing;
using CacheCow.SharedKernel;
using Microsoft.Extensions.Options;

namespace CacheCow.Host.Composition.Seo;

/// <summary>
/// The anonymous, machine-consumed SEO routes (issue 071): the sitemap index,
/// one sitemap per launch market, and one product feed per launch market. The
/// bodies are composed by <see cref="SeoSurfaceComposer"/>, which runs every
/// SKU and content experience through the REAL Market &amp; Gating enforcement
/// point; this file owns only the HTTP boundary concerns:
///
/// <list type="bullet">
/// <item>The transacting market is read from the request PATH — server-addressed
/// state — and resolved exact-match against the six launch markets. No
/// Accept-Language, geolocation, or cookie can reach the decision, because no
/// code here reads one (CC-SEC-012; SECURITY.md, Authentication rule 10). An
/// unknown market slug is 404 (CC-MKT-001 discipline).</item>
/// <item>Caching follows the module's <see cref="ResponseCachePolicy"/>
/// primitive: these responses are <see cref="ResponseClass.AnonymousMarketGated"/>,
/// cacheable only under the market+locale key the policy derives from the
/// server-side context (CC-MKT-009, CC-SEC-013; SECURITY.md, HTTP boundary
/// rule 10). Nothing per-user, no Set-Cookie, and no <c>Vary</c> on a client
/// hint — the market lives in the URL, so the URL is the key.</item>
/// <item>The base URL is REQUIRED configuration and fails closed with a 503
/// problem response: production hostnames are an undecided fact
/// (ARCHITECTURE.md, "Known unknowns"), and no hostname is invented here
/// (see <see cref="SeoOptions"/>).</item>
/// </list>
///
/// Fail-closed generation: each document is composed COMPLETELY in memory
/// before a single byte is written, so a gating fault mid-generation aborts to
/// the host's 5xx problem handler having emitted nothing — a partial or
/// ungated body is unreachable (issue 071 Failure Behavior; SECURITY.md,
/// Logging rule 2).
/// </summary>
public static class SeoEndpoints
{
    private const string XmlContentType = "application/xml; charset=utf-8";

    public static IEndpointRouteBuilder MapSeoSurfaces(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // Anonymous by explicit opt-out from the deny-by-default fallback
        // policy (SECURITY.md, Authentication rule 1): crawlers and feed
        // consumers are unauthenticated public traffic, and these surfaces
        // carry public catalog/URL data only (issue 071, Data Classification).
        endpoints.MapGet(SeoUrlScheme.SitemapIndexPath, SitemapIndex).AllowAnonymous();
        endpoints.MapGet(SeoUrlScheme.SitemapRouteTemplate, MarketSitemap).AllowAnonymous();
        endpoints.MapGet(SeoUrlScheme.FeedRouteTemplate, MarketFeed).AllowAnonymous();

        return endpoints;
    }

    /// <summary>
    /// The sitemap index: the six per-market sitemap locations, identical for
    /// every requester and carrying no market-variant content — hence no
    /// <see cref="TransactingContext"/>. <see cref="CacheDirective"/> cannot
    /// express "cacheable without a market key" by construction (CC-MKT-009),
    /// so the index is served no-store rather than inventing an unkeyed
    /// cacheable class here; a cache class for market-invariant public
    /// documents would be a policy decision for the owning module, not a
    /// choice this endpoint may make.
    /// </summary>
    private static IResult SitemapIndex(
        HttpContext http,
        SeoSurfaceComposer composer,
        IOptions<SeoOptions> options)
    {
        if (!SeoOptions.TryGetValidatedBaseUrl(options.Value.BaseUrl, out var baseUrl))
        {
            return Unconfigured(http);
        }

        ApplyCachePolicy(http, options.Value, gatingContext: null);
        return Xml(composer.SitemapIndex(baseUrl));
    }

    /// <summary>One market's gated sitemap; the market comes from the path only.</summary>
    private static IResult MarketSitemap(
        HttpContext http,
        string market,
        SeoSurfaceComposer composer,
        IOptions<SeoOptions> options)
    {
        if (!SeoUrlScheme.TryResolveMarketSlug(market, out var resolved))
        {
            return NotFound(http);
        }

        if (!SeoOptions.TryGetValidatedBaseUrl(options.Value.BaseUrl, out var baseUrl))
        {
            return Unconfigured(http);
        }

        ApplyCachePolicy(http, options.Value, GatingContextFor(resolved));
        return Xml(composer.MarketSitemap(baseUrl, resolved));
    }

    /// <summary>
    /// One market's gated product feed. The canonical <see cref="PriceList"/>
    /// is operational data whose durable store is a later persistence issue and
    /// which the shipped configuration does not register; without it there are
    /// no offers to publish, so the feed fails closed with 503 rather than
    /// emitting a priceless or empty feed that a consumer would read as
    /// "this market sells nothing" (CC-PRC-001; SECURITY.md, Logging rule 2).
    /// </summary>
    private static IResult MarketFeed(
        HttpContext http,
        string market,
        SeoSurfaceComposer composer,
        IOptions<SeoOptions> options)
    {
        if (!SeoUrlScheme.TryResolveMarketSlug(market, out var resolved))
        {
            return NotFound(http);
        }

        if (!SeoOptions.TryGetValidatedBaseUrl(options.Value.BaseUrl, out var baseUrl))
        {
            return Unconfigured(http);
        }

        var prices = http.RequestServices.GetService<PriceList>();
        if (prices is null)
        {
            return Unconfigured(http);
        }

        ApplyCachePolicy(http, options.Value, GatingContextFor(resolved));
        return Xml(composer.ProductFeed(baseUrl, resolved, prices));
    }

    /// <summary>
    /// The server-side transacting context for a path-addressed market. The
    /// carrier locale is the same deterministic structural value the rest of
    /// host gating uses; it is never a user-facing language choice and never
    /// influences a gating outcome (see <see cref="SkuGating.CarrierLocaleFor"/>).
    /// </summary>
    private static TransactingContext GatingContextFor(Market market) =>
        new(market, SkuGating.CarrierLocaleFor(market));

    /// <summary>
    /// Applies the caching decision the MarketGating module owns. Only an
    /// <see cref="ResponseClass.AnonymousMarketGated"/> response with a
    /// resolved server-side context is cacheable, and only when deployment has
    /// configured an explicit lifetime; every other combination — including an
    /// unset lifetime — is no-store. No <c>Vary</c> header is emitted: varying
    /// on a client hint is exactly the cross-market cache-reuse hole
    /// CC-MKT-009 closes, and the market is already in the path.
    /// </summary>
    private static void ApplyCachePolicy(HttpContext http, SeoOptions options, TransactingContext? gatingContext)
    {
        var directive = ResponseCachePolicy.Classify(ResponseClass.AnonymousMarketGated, gatingContext);
        var maxAge = options.PublicMaxAgeSeconds;

        http.Response.Headers.CacheControl = directive.MayBeCached && maxAge > 0
            ? string.Create(CultureInfo.InvariantCulture, $"public, max-age={maxAge}")
            : CacheDirective.NoStoreHeaderValue;
    }

    /// <summary>Serializes a composed document; XML is written by <see cref="XmlWriter"/>, never assembled as strings (SECURITY.md, Input validation rule 9).</summary>
    private static IResult Xml(XDocument document)
    {
        using var buffer = new MemoryStream();
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
        };

        using (var writer = XmlWriter.Create(buffer, settings))
        {
            document.Save(writer);
        }

        return Results.Bytes(buffer.ToArray(), XmlContentType);
    }

    /// <summary>
    /// Required configuration is absent or invalid. Generic RFC 9457 body: the
    /// client learns nothing about which setting is missing (SECURITY.md,
    /// Logging rule 1). Never cached — a 503 must not be pinned at an edge.
    /// </summary>
    private static IResult Unconfigured(HttpContext http)
    {
        http.Response.Headers.CacheControl = CacheDirective.NoStoreHeaderValue;
        return Results.Problem(title: "Service unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    private static IResult NotFound(HttpContext http)
    {
        http.Response.Headers.CacheControl = CacheDirective.NoStoreHeaderValue;
        return Results.Problem(title: "Not found.", statusCode: StatusCodes.Status404NotFound);
    }
}
