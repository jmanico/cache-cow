using System.Diagnostics.CodeAnalysis;

namespace CacheCow.Host.Composition.Seo;

/// <summary>
/// Host configuration for the SEO surfaces (sitemaps, feeds, structured data).
///
/// <see cref="BaseUrl"/> is REQUIRED for any surface that emits absolute URLs:
/// the production hostnames are an undecided engineering fact (ARCHITECTURE.md,
/// "Known unknowns" discipline — no hostname is invented here), so the shipped
/// configuration carries no value and every dependent endpoint fails closed
/// with a 503 problem response until deployment supplies one (issue 071
/// Failure Behavior: ungated/misaddressed output is never emitted). Whether a
/// single canonical host or one host per market is used is likewise open;
/// this option models the single-host shape and is trivially extensible to a
/// per-market map once a human decides.
/// </summary>
public sealed class SeoOptions
{
    public const string SectionName = "Seo";

    /// <summary>
    /// Absolute https base URL for sitemap/feed/hreflang URLs, e.g.
    /// "https://www.example.test". No default exists (see class remarks).
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Explicit public cache lifetime for the anonymous, market-keyed
    /// sitemap/feed responses (CC-MKT-009: cacheable only under the
    /// market+locale key). Zero or unset means the responses are served
    /// no-store — the fail-closed default; no lifetime is invented.
    /// </summary>
    public int PublicMaxAgeSeconds { get; set; }

    /// <summary>
    /// Validates the configured base URL against the scheme allowlist
    /// discipline of SECURITY.md, Input validation rule 6: absolute, https
    /// only, no query/fragment/user-info. Anything else is rejected — the
    /// endpoints fail closed rather than emitting URLs on a malformed or
    /// non-https base.
    /// </summary>
    internal static bool TryGetValidatedBaseUrl(string? configured, [NotNullWhen(true)] out Uri? baseUrl)
    {
        baseUrl = null;
        if (string.IsNullOrWhiteSpace(configured)
            || !Uri.TryCreate(configured, UriKind.Absolute, out var candidate)
            || !string.Equals(candidate.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            || !string.IsNullOrEmpty(candidate.Query)
            || !string.IsNullOrEmpty(candidate.Fragment)
            || !string.IsNullOrEmpty(candidate.UserInfo))
        {
            return false;
        }

        baseUrl = candidate;
        return true;
    }
}
