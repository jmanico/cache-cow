using System.Globalization;
using System.Security.Claims;

namespace CacheCow.Modules.WholesaleB2B.Auth;

/// <summary>
/// Claim extraction shared by the token policy validator and the rate-limit
/// partition provider. Reads only — all trust decisions live in
/// <see cref="B2BTokenClaimsValidator"/>.
/// </summary>
internal static class B2BTokenClaims
{
    /// <summary>Client-identifier claim types, in precedence order (RFC 8693 <c>client_id</c>, Entra <c>azp</c>/<c>appid</c>).</summary>
    private static readonly string[] ClientIdClaimTypes = ["client_id", "azp", "appid"];

    private static readonly char[] ScopeSeparators = [' '];

    internal static string? ClientId(ClaimsPrincipal principal)
    {
        foreach (var claimType in ClientIdClaimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>All scope values across <c>scp</c>/<c>scope</c> claims (space-separated per RFC 6749 §3.3).</summary>
    internal static IReadOnlyList<string> ScopeValues(ClaimsPrincipal principal)
    {
        var values = new List<string>();
        foreach (var claim in principal.Claims)
        {
            if (claim.Type is "scp" or "scope")
            {
                values.AddRange(claim.Value.Split(ScopeSeparators, StringSplitOptions.RemoveEmptyEntries));
            }
        }

        return values;
    }

    internal static long? UnixSeconds(ClaimsPrincipal principal, string claimType) =>
        long.TryParse(principal.FindFirst(claimType)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            ? seconds
            : null;

    internal static string? ConfirmationClaim(ClaimsPrincipal principal) =>
        principal.FindFirst("cnf")?.Value;
}
