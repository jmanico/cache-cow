namespace CacheCow.Modules.OrderingPayments.Idempotency;

/// <summary>How a <see cref="IIdempotencyStore.Claim"/> resolved.</summary>
public enum IdempotencyClaimStatus
{
    /// <summary>This caller atomically won the (scope, key) reservation and MUST run the operation, then <see cref="IIdempotencyStore.Complete"/> or <see cref="IIdempotencyStore.Release"/>.</summary>
    Winner = 0,

    /// <summary>The (scope, key) already completed with a matching fingerprint; <see cref="IdempotencyClaim.StoredResult"/> is the original result (CC-ORD-005).</summary>
    Replay = 1,

    /// <summary>The (scope, key) exists with a DIFFERENT fingerprint: reject with 409 — never the original result, never a new order (CC-SEC-015).</summary>
    FingerprintConflict = 2,
}

/// <summary>Resolution of a claim; <see cref="StoredResult"/> is non-null exactly when <see cref="Status"/> is <see cref="IdempotencyClaimStatus.Replay"/>.</summary>
public sealed class IdempotencyClaim
{
    private IdempotencyClaim(IdempotencyClaimStatus status, object? storedResult)
    {
        Status = status;
        StoredResult = storedResult;
    }

    public IdempotencyClaimStatus Status { get; }

    public object? StoredResult { get; }

    public static IdempotencyClaim Winner() => new(IdempotencyClaimStatus.Winner, null);

    public static IdempotencyClaim Replay(object storedResult)
    {
        ArgumentNullException.ThrowIfNull(storedResult);
        return new IdempotencyClaim(IdempotencyClaimStatus.Replay, storedResult);
    }

    public static IdempotencyClaim FingerprintConflict() => new(IdempotencyClaimStatus.FingerprintConflict, null);
}

/// <summary>
/// Port for durable idempotency state (CC-ORD-005, CC-API-005, CC-SEC-015;
/// durable PostgreSQL implementation with a uniqueness constraint on
/// (scope, key) is a persistence issue — the in-memory implementation here is
/// for tests and pre-persistence wiring).
///
/// CONTRACT (atomic reserve/claim semantics — implementations MUST honor all
/// of it):
/// 1. Entries are keyed by (scope, key), never by key alone (CC-SEC-015). A
///    key presented in a different scope is a fresh key in that scope.
/// 2. <see cref="Claim"/> is atomic: among concurrent claims for the same
///    (scope, key) with the same fingerprint, exactly ONE caller receives
///    <see cref="IdempotencyClaimStatus.Winner"/>; every other caller receives
///    <see cref="IdempotencyClaimStatus.Replay"/> with the winner's completed
///    result (implementations may block or retry internally until the
///    in-flight winner completes or releases — e.g. via a database unique
///    index plus row lock).
/// 3. A claim whose fingerprint differs from the stored entry's — in-flight
///    or completed — resolves to
///    <see cref="IdempotencyClaimStatus.FingerprintConflict"/> immediately.
/// 4. The winner MUST call <see cref="Complete"/> after the operation
///    succeeds; the stored result replays for the configured retention window
///    (window duration is an open decision, <see cref="IdempotencyOptions"/>),
///    after which the entry expires and the key may be reused as fresh.
/// 5. The winner MUST call <see cref="Release"/> if the operation fails
///    without its side effect (no order created): the reservation is removed
///    so a retry is not poisoned into replaying a failure (issue 037, AC-07).
/// 6. Any store failure throws — callers fail closed and deny processing
///    rather than proceeding unprotected (SECURITY.md, Logging rule 2).
/// </summary>
public interface IIdempotencyStore
{
    IdempotencyClaim Claim(IdempotencyScope scope, IdempotencyKey key, RequestFingerprint fingerprint);

    void Complete(IdempotencyScope scope, IdempotencyKey key, object result);

    void Release(IdempotencyScope scope, IdempotencyKey key);
}
