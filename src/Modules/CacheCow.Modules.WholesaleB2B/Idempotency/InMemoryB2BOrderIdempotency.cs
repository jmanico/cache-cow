namespace CacheCow.Modules.WholesaleB2B.Idempotency;

/// <summary>
/// In-memory <see cref="IB2BOrderIdempotency"/> for tests and pre-persistence
/// wiring. Honors the (client, key) scoping and fingerprint-binding contract;
/// one deliberate narrowing: a concurrent duplicate that arrives while the
/// winner is still in flight resolves to
/// <see cref="B2BIdempotencyStatus.FingerprintConflict"/> instead of blocking
/// for the winner's result — fail closed, no duplicate order is possible
/// (CC-SEC-015). The durable host adapter must implement the full
/// block-and-replay semantics of the Ordering &amp; Payments store.
/// </summary>
public sealed class InMemoryB2BOrderIdempotency : IB2BOrderIdempotency
{
    private sealed record Entry(string Fingerprint, string? OrderId)
    {
        public bool Completed => OrderId is not null;
    }

    private readonly Lock _gate = new();
    private readonly Dictionary<(string ClientId, string Key), Entry> _entries = [];

    public B2BIdempotencyClaim Claim(string clientId, string idempotencyKey, string requestFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestFingerprint);

        lock (_gate)
        {
            if (!_entries.TryGetValue((clientId, idempotencyKey), out var entry))
            {
                _entries[(clientId, idempotencyKey)] = new Entry(requestFingerprint, null);
                return B2BIdempotencyClaim.Accepted();
            }

            if (!string.Equals(entry.Fingerprint, requestFingerprint, StringComparison.Ordinal))
            {
                return B2BIdempotencyClaim.Conflict();
            }

            return entry.Completed
                ? B2BIdempotencyClaim.Replay(entry.OrderId!)
                : B2BIdempotencyClaim.Conflict(); // in-flight duplicate: fail closed (see class docs)
        }
    }

    public void Complete(string clientId, string idempotencyKey, string requestFingerprint, string orderId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);

        lock (_gate)
        {
            if (_entries.TryGetValue((clientId, idempotencyKey), out var entry)
                && string.Equals(entry.Fingerprint, requestFingerprint, StringComparison.Ordinal)
                && !entry.Completed)
            {
                _entries[(clientId, idempotencyKey)] = entry with { OrderId = orderId };
            }
        }
    }

    public void Release(string clientId, string idempotencyKey)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue((clientId, idempotencyKey), out var entry) && !entry.Completed)
            {
                _entries.Remove((clientId, idempotencyKey));
            }
        }
    }
}
