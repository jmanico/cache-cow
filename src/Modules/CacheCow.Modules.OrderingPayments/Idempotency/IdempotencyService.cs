namespace CacheCow.Modules.OrderingPayments.Idempotency;

/// <summary>The outcome of an idempotent execution: the (possibly replayed) result.</summary>
/// <typeparam name="TResult">The operation's result type (e.g. the order aggregate).</typeparam>
public sealed class IdempotentResult<TResult>
    where TResult : class
{
    internal IdempotentResult(TResult value, bool wasReplay)
    {
        Value = value;
        WasReplay = wasReplay;
    }

    public TResult Value { get; }

    /// <summary>True when the stored original result was returned instead of executing the operation (CC-ORD-005).</summary>
    public bool WasReplay { get; }
}

/// <summary>
/// Idempotent execution wrapper for order creation (CC-ORD-005, CC-API-005,
/// CC-SEC-015), reusable by the consumer submission flow (issue 036) and the
/// B2B order endpoints (issues 053/055; the header requirement and 400 on a
/// missing header live at the HTTP surface).
///
/// Semantics: keys are scoped to the issuing tenant/account/guest session and
/// bound to a fingerprint of the original request. Replay with a matching
/// fingerprint returns the stored original result without re-executing; the
/// same key with a different fingerprint throws
/// <see cref="IdempotencyConflictException"/> (409) — never the original
/// silently, never a second execution. Concurrent duplicates collapse to
/// exactly one execution (store contract). Any store failure propagates:
/// processing is denied rather than running unprotected (fail closed;
/// SECURITY.md, Logging rule 2).
/// </summary>
public sealed class IdempotencyService
{
    private readonly IIdempotencyStore _store;
    private readonly IRequestFingerprintStrategy _fingerprintStrategy;

    public IdempotencyService(IIdempotencyStore store, IRequestFingerprintStrategy fingerprintStrategy)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(fingerprintStrategy);
        _store = store;
        _fingerprintStrategy = fingerprintStrategy;
    }

    /// <summary>
    /// Executes <paramref name="operation"/> at most once per
    /// (scope, key, fingerprint). The fingerprint is computed over
    /// <paramref name="requestContent"/> — the received request content before
    /// any mutation (canonicalization is an open question, issue 037). If the
    /// operation throws, the reservation is released so a retry can process
    /// (idempotency protects side effects, not transient failures) and the
    /// exception propagates.
    /// </summary>
    public IdempotentResult<TResult> Execute<TResult>(
        IdempotencyScope scope,
        IdempotencyKey key,
        ReadOnlySpan<byte> requestContent,
        Func<TResult> operation)
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(operation);

        var fingerprint = _fingerprintStrategy.ComputeFingerprint(requestContent);
        var claim = _store.Claim(scope, key, fingerprint);

        switch (claim.Status)
        {
            case IdempotencyClaimStatus.Replay:
                return new IdempotentResult<TResult>((TResult)claim.StoredResult!, wasReplay: true);

            case IdempotencyClaimStatus.FingerprintConflict:
                throw new IdempotencyConflictException(scope, key);

            case IdempotencyClaimStatus.Winner:
                try
                {
                    var result = operation();
                    _store.Complete(scope, key, result);
                    return new IdempotentResult<TResult>(result, wasReplay: false);
                }
                catch
                {
                    _store.Release(scope, key);
                    throw;
                }

            default:
                throw new InvalidOperationException($"Unknown idempotency claim status {claim.Status}.");
        }
    }
}
