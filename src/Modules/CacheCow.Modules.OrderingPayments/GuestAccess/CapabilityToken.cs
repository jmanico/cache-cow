using CacheCow.Modules.OrderingPayments.Orders;

namespace CacheCow.Modules.OrderingPayments.GuestAccess;

/// <summary>
/// A freshly issued guest capability token (issue 042; CC-ORD-010,
/// CC-SEC-017): ≥ 128 bits of cryptographic-RNG entropy, bound to exactly one
/// order and one purpose, expiring, server-revocable. Produced only by
/// <see cref="GuestAccessTokenService.Issue"/>.
///
/// The secret exists ONLY here, transiently, so it can be placed into the
/// guest's link (issue 043); the store keeps a SHA-256 digest, never the
/// secret. The secret is exposed through the <see cref="RevealSecret"/>
/// METHOD — deliberately not a property, so object serializers and log
/// destructuring never pick it up — and <see cref="ToString"/> is redacted
/// (SECURITY.md, Authentication rule 14; Logging rule 4).
/// </summary>
public sealed class CapabilityToken
{
    private readonly string _secret;

    internal CapabilityToken(
        string secret,
        CapabilityTokenDigest digest,
        OrderId orderId,
        GuestAccessPurpose purpose,
        DateTimeOffset expiresAt)
    {
        _secret = secret;
        Digest = digest;
        OrderId = orderId;
        Purpose = purpose;
        ExpiresAt = expiresAt;
    }

    /// <summary>Digest handle for targeted server-side revocation.</summary>
    public CapabilityTokenDigest Digest { get; }

    /// <summary>The exactly-one order this token is bound to (CC-ORD-010).</summary>
    public OrderId OrderId { get; }

    /// <summary>The exactly-one purpose this token is bound to.</summary>
    public GuestAccessPurpose Purpose { get; }

    public DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// The opaque, URL-safe token secret (base64url) for the guest link.
    /// Handle as a secret: HTTPS-only, never logged, never in analytics query
    /// strings, kept out of Referer (SECURITY.md, Authentication rule 14).
    /// </summary>
    public string RevealSecret() => _secret;

    /// <summary>Redacted on purpose — the secret never leaks through formatting (SECURITY.md, Logging rule 4).</summary>
    public override string ToString() => "CapabilityToken[redacted]";
}
