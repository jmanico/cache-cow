using System.Buffers.Text;
using System.Security.Cryptography;
using CacheCow.Modules.OrderingPayments.Orders;

namespace CacheCow.Modules.OrderingPayments.GuestAccess;

/// <summary>
/// Issues and validates the single-purpose capability tokens gating guest
/// access to order status, tracking, and invoice download (issue 042;
/// CC-ORD-010, CC-SEC-017, CC-INV-002; SECURITY.md, Authentication rule 14).
///
/// Issue: 256 bits from the cryptographic RNG (twice the mandated 128-bit
/// floor), base64url-encoded — opaque and URL-safe. Only the SHA-256 digest
/// is stored; the secret exists transiently in the returned
/// <see cref="CapabilityToken"/> for link construction and nowhere else.
///
/// Validate: the presented secret is attacker-controlled input; its digest is
/// looked up and compared in constant time against the stored digest, then
/// revocation, expiry, and the exactly-one-order/one-purpose binding are
/// checked. Every failure — malformed, unknown, expired, revoked, wrong
/// purpose — yields the SAME indistinguishable "not valid" outcome, so the
/// caller (and thus an attacker probing the endpoint) learns nothing about
/// WHY (the endpoint answers 404 either way; SECURITY.md, Authentication
/// rule 9). Any exception in the validation path is a denial, never a bypass
/// (SECURITY.md, Logging rule 2).
///
/// There is deliberately NO lookup by order number, email, or any
/// combination of enumerable identifiers — no such method exists
/// (CC-ORD-010). Resolution goes FROM token TO order, so guest links never
/// carry an order identifier as the access key (CC-INV-002).
/// Authenticated-account access uses object-level authorization instead,
/// never this path (CC-SEC-017).
/// </summary>
public sealed class GuestAccessTokenService
{
    /// <summary>Secret size: 32 bytes = 256 bits, ≥ the 128-bit mandate (CC-ORD-010).</summary>
    public const int SecretByteLength = 32;

    /// <summary>Floor below which a presented value can never be a Cache Cow token (128 bits).</summary>
    private const int MinimumSecretByteLength = 16;

    /// <summary>Bound on attacker-controlled presented input (SECURITY.md, Input validation rule 1).</summary>
    private const int MaxPresentedLength = 256;

    private readonly ICapabilityTokenStore _store;
    private readonly GuestAccessOptions _options;
    private readonly TimeProvider _timeProvider;

    public GuestAccessTokenService(
        ICapabilityTokenStore store,
        GuestAccessOptions options,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(options);
        _store = store;
        _options = options;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Issues a fresh capability token bound to exactly this order and
    /// purpose, expiring after the configured lifetime. The store receives
    /// only the digest.
    /// </summary>
    public CapabilityToken Issue(OrderId orderId, GuestAccessPurpose purpose)
    {
        var secret = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SecretByteLength));
        var digest = CapabilityTokenDigest.Compute(secret);
        var expiresAt = _timeProvider.GetUtcNow() + _options.TokenLifetime;

        _store.Add(new CapabilityTokenRecord(digest, orderId, purpose, expiresAt, Revoked: false));

        return new CapabilityToken(secret, digest, orderId, purpose, expiresAt);
    }

    /// <summary>
    /// Validates a presented token for the given purpose and resolves the one
    /// order it is bound to. Returns false — with no distinguishing detail —
    /// for anything else: malformed, unknown, revoked, expired, or
    /// wrong-purpose presentations all look identical to the caller.
    /// </summary>
    public bool TryAuthorize(string? presentedSecret, GuestAccessPurpose purpose, out OrderId orderId)
    {
        orderId = default;

        try
        {
            if (string.IsNullOrWhiteSpace(presentedSecret) || presentedSecret.Length > MaxPresentedLength)
            {
                return false;
            }

            byte[] decoded;
            try
            {
                decoded = Base64Url.DecodeFromChars(presentedSecret);
            }
            catch (FormatException)
            {
                return false;
            }

            if (decoded.Length < MinimumSecretByteLength)
            {
                return false;
            }

            var digest = CapabilityTokenDigest.Compute(presentedSecret);
            var record = _store.Find(digest);
            if (record is null
                || !digest.FixedTimeEquals(record.Digest)
                || record.Revoked
                || _timeProvider.GetUtcNow() >= record.ExpiresAt
                || record.Purpose != purpose)
            {
                return false;
            }

            orderId = record.OrderId;
            return true;
        }
        catch (Exception)
        {
            // Fail closed: any exception in token validation is a denial,
            // never a bypass (issue 042, Failure Behavior; SECURITY.md,
            // Logging rule 2).
            orderId = default;
            return false;
        }
    }

    /// <summary>
    /// True only when the token is valid for this purpose AND bound to
    /// exactly this order; a token for a different order is simply not valid
    /// (the endpoint answers 404, revealing nothing — issue 042, AC-04).
    /// </summary>
    public bool IsAuthorizedFor(string? presentedSecret, GuestAccessPurpose purpose, OrderId orderId) =>
        TryAuthorize(presentedSecret, purpose, out var boundOrder) && boundOrder == orderId;

    /// <summary>Server-side revocation of one token by digest (the secret is not needed to revoke).</summary>
    public void Revoke(CapabilityTokenDigest digest) => _store.Revoke(digest);

    /// <summary>Server-side revocation of every token bound to an order (e.g. on erasure request or abuse signal).</summary>
    public void RevokeAllForOrder(OrderId orderId) => _store.RevokeAllForOrder(orderId);
}
