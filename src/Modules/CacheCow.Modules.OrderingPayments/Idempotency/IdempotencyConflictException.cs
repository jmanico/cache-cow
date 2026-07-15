namespace CacheCow.Modules.OrderingPayments.Idempotency;

/// <summary>
/// The same idempotency key was presented in the same scope with a different
/// request fingerprint (CC-SEC-015; SECURITY.md, Input validation rule 12).
/// Maps to HTTP 409 with RFC 9457 problem details at the API surface
/// (issue 021), and is a structured security event — a probing/tampering
/// signal (SECURITY.md, Logging rule 3). Neither the stored result nor fresh
/// processing ever occurs.
/// </summary>
public sealed class IdempotencyConflictException : Exception
{
    public IdempotencyConflictException(IdempotencyScope scope, IdempotencyKey key)
        : base(
            $"Idempotency key '{key}' was already used in scope {scope} with a different request; "
            + "replays must match the original request exactly (CC-SEC-015).")
    {
        Scope = scope;
        Key = key;
    }

    public IdempotencyScope Scope { get; }

    public IdempotencyKey Key { get; }
}
