namespace CacheCow.Host.Security;

/// <summary>
/// Validates that a configured origin is a single exact HTTPS origin -
/// scheme://host[:port] with nothing else. Wildcards, suffix/pattern matches,
/// paths, userinfo, query strings and plaintext schemes are all rejected
/// (SECURITY.md, HTTP boundary rules 2 and 4: specific origins only, never
/// wildcards or suffix matches - suffix matching is bypassable).
/// </summary>
public static class ExactOrigin
{
    public static bool IsValid(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        if (origin.Contains('*', StringComparison.Ordinal))
        {
            return false;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        // The configured string must be exactly the origin (no path, query,
        // fragment or trailing slash), so exact string comparison at match
        // time can never be widened into a prefix/suffix match.
        return string.Equals(origin, uri.GetLeftPart(UriPartial.Authority), StringComparison.Ordinal);
    }
}
