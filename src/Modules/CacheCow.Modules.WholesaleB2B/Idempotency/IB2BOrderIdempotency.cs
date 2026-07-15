namespace CacheCow.Modules.WholesaleB2B.Idempotency;

/// <summary>How a claim for (client, Idempotency-Key) resolved.</summary>
public enum B2BIdempotencyStatus
{
    /// <summary>This caller won the reservation and MUST create the order, then <see cref="IB2BOrderIdempotency.Complete"/> or <see cref="IB2BOrderIdempotency.Release"/>.</summary>
    Accepted = 0,

    /// <summary>The key already completed with a matching request fingerprint; <see cref="B2BIdempotencyClaim.StoredOrderId"/> is the original result (CC-API-005).</summary>
    Replay = 1,

    /// <summary>The key exists with a DIFFERENT fingerprint: 409, never the original result, never a new order (CC-SEC-015).</summary>
    FingerprintConflict = 2,
}

/// <summary><see cref="StoredOrderId"/> is non-null exactly when <see cref="Status"/> is <see cref="B2BIdempotencyStatus.Replay"/>.</summary>
public sealed class B2BIdempotencyClaim
{
    private B2BIdempotencyClaim(B2BIdempotencyStatus status, string? storedOrderId)
    {
        Status = status;
        StoredOrderId = storedOrderId;
    }

    public B2BIdempotencyStatus Status { get; }

    public string? StoredOrderId { get; }

    internal static B2BIdempotencyClaim Accepted() => new(B2BIdempotencyStatus.Accepted, null);

    internal static B2BIdempotencyClaim Replay(string storedOrderId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storedOrderId);
        return new B2BIdempotencyClaim(B2BIdempotencyStatus.Replay, storedOrderId);
    }

    internal static B2BIdempotencyClaim Conflict() => new(B2BIdempotencyStatus.FingerprintConflict, null);
}

/// <summary>
/// Port carrying the Ordering &amp; Payments idempotency concepts into the B2B
/// surface without a module reference (CC-API-005, CC-ORD-005, CC-SEC-015;
/// ARCHITECTURE.md, Dependency rule 9 — cross-context needs are ports). The
/// host adapts this onto the shared durable idempotency store.
///
/// CONTRACT:
/// 1. Entries key on (clientId, idempotencyKey) — never on the key alone —
///    so one partner's key can never collide with, or read, another's
///    (CC-SEC-015). The clientId comes exclusively from the validated
///    <see cref="Auth.B2BClientContext"/>, never from a request field.
/// 2. Every entry is bound to a fingerprint of the original request. A replay
///    with the same key and same fingerprint returns the original order id; a
///    request with the same key and a DIFFERENT fingerprint resolves to
///    <see cref="B2BIdempotencyStatus.FingerprintConflict"/> (409) — never
///    silently served the original, never processed as a new order.
/// 3. Durable implementations honor the atomic winner semantics of the
///    Ordering &amp; Payments store (exactly one concurrent winner; losers
///    replay the winner's completed result). Retention window per CC-API-005
///    is that store's policy.
/// </summary>
public interface IB2BOrderIdempotency
{
    B2BIdempotencyClaim Claim(string clientId, string idempotencyKey, string requestFingerprint);

    void Complete(string clientId, string idempotencyKey, string requestFingerprint, string orderId);

    /// <summary>Releases a reservation whose operation failed, so a retry may run.</summary>
    void Release(string clientId, string idempotencyKey);
}
