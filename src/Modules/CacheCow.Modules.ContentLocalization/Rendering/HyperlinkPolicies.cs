namespace CacheCow.Modules.ContentLocalization.Rendering;

/// <summary>
/// Resolves an untrusted, data-derived link destination to a safe href value.
/// Implementations MUST allowlist, never denylist, and fall back to "#" on
/// anything outside the allowlist (CC-SEC-004; SECURITY.md, Input validation
/// rule 6).
/// </summary>
public interface IHyperlinkUrlPolicy
{
    /// <summary>Returns a safe href value, or "#" when the destination fails validation.</summary>
    string Resolve(string? destination);
}

/// <summary>
/// The default policy: absolute URLs whose scheme is https, mailto, or tel
/// pass; everything else — javascript:, data:, http:, relative or
/// unparseable input — resolves to "#" (CC-SEC-004).
/// </summary>
public sealed class SchemeAllowlistUrlPolicy : IHyperlinkUrlPolicy
{
    public const string Fallback = "#";

    private static readonly string[] AllowedSchemes = [Uri.UriSchemeHttps, Uri.UriSchemeMailto, "tel"];

    public string Resolve(string? destination)
    {
        if (string.IsNullOrWhiteSpace(destination))
        {
            return Fallback;
        }

        if (!Uri.TryCreate(destination, UriKind.Absolute, out var uri))
        {
            return Fallback;
        }

        return AllowedSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase)
            ? uri.AbsoluteUri
            : Fallback;
    }
}

/// <summary>
/// Configuration port for the registered-domain allowlist used to validate
/// partner and locator links (CC-SEC-004; SECURITY.md, Input validation
/// rule 6). The domain list is configuration, not code; the config adapter
/// lands with a later issue.
/// </summary>
public interface IRegisteredDomainAllowlist
{
    /// <summary>True when the host is the registered domain or a subdomain of it (label-boundary match).</summary>
    bool IsAllowed(string host);
}

/// <summary>In-memory allowlist for tests and the provisional module default (empty = nothing allowed; fail closed).</summary>
public sealed class InMemoryRegisteredDomainAllowlist : IRegisteredDomainAllowlist
{
    private readonly IReadOnlyList<string> _registeredDomains;

    public InMemoryRegisteredDomainAllowlist(IReadOnlyList<string> registeredDomains)
    {
        ArgumentNullException.ThrowIfNull(registeredDomains);
        _registeredDomains = registeredDomains
            .Select(d => d.Trim().TrimStart('.').ToLowerInvariant())
            .Where(d => d.Length > 0)
            .ToArray();
    }

    public bool IsAllowed(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        var normalized = host.ToLowerInvariant();
        foreach (var domain in _registeredDomains)
        {
            if (string.Equals(normalized, domain, StringComparison.Ordinal)
                || normalized.EndsWith("." + domain, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Policy for partner/locator links: https only AND the host must sit under a
/// registered domain on the configured allowlist; anything else resolves to
/// "#" (CC-SEC-004). mailto/tel are not partner-link schemes and fail here.
/// </summary>
public sealed class PartnerLinkUrlPolicy : IHyperlinkUrlPolicy
{
    private readonly IRegisteredDomainAllowlist _allowlist;

    public PartnerLinkUrlPolicy(IRegisteredDomainAllowlist allowlist)
    {
        ArgumentNullException.ThrowIfNull(allowlist);
        _allowlist = allowlist;
    }

    public string Resolve(string? destination)
    {
        if (string.IsNullOrWhiteSpace(destination)
            || !Uri.TryCreate(destination, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !_allowlist.IsAllowed(uri.Host))
        {
            return SchemeAllowlistUrlPolicy.Fallback;
        }

        return uri.AbsoluteUri;
    }
}
