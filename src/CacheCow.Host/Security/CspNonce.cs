using System.Security.Cryptography;

namespace CacheCow.Host.Security;

/// <summary>
/// Per-response CSP nonce (SECURITY.md, HTTP boundary rule 2). 128 bits of
/// cryptographic-RNG entropy, generated once per request by
/// <see cref="SecurityHeadersMiddleware"/> and readable by server rendering
/// (e.g. the future Angular SSR document) via <see cref="Get"/>. If nonce
/// generation fails the exception propagates and the response is never served
/// with a weakened or absent CSP (issue 017, "On System Error").
/// </summary>
public static class CspNonce
{
    private const string ItemKey = "CacheCow.Security.CspNonce";

    public static string Issue(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        context.Items[ItemKey] = nonce;
        return nonce;
    }

    public static string? Get(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Items.TryGetValue(ItemKey, out var value) ? value as string : null;
    }
}
