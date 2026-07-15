using System.Collections.Concurrent;
using CacheCow.Modules.OrderingPayments.Orders;

namespace CacheCow.Modules.OrderingPayments.GuestAccess;

/// <summary>
/// The persisted shape of a capability token (issue 042): digest, binding,
/// expiry, revocation flag — deliberately NO field for the token secret, so
/// storing the secret is unrepresentable (SECURITY.md, Authentication
/// rule 14). Lives in the Ordering context's own schema under its
/// least-privilege role once persistence lands (SECURITY.md, Secret handling
/// rule 10).
/// </summary>
public sealed record CapabilityTokenRecord(
    CapabilityTokenDigest Digest,
    OrderId OrderId,
    GuestAccessPurpose Purpose,
    DateTimeOffset ExpiresAt,
    bool Revoked);

/// <summary>
/// Port for capability-token persistence (issue 042; the durable PostgreSQL
/// store is a persistence issue). Contract: records are keyed by digest;
/// revocation marks a record revoked (kept, so a revoked token stays
/// deniable) and is idempotent; any store failure throws — callers fail
/// closed and deny access (SECURITY.md, Logging rule 2).
/// </summary>
public interface ICapabilityTokenStore
{
    void Add(CapabilityTokenRecord record);

    /// <summary>The record for this digest, or null when none exists.</summary>
    CapabilityTokenRecord? Find(CapabilityTokenDigest digest);

    /// <summary>Marks one token revoked; unknown digests are a no-op.</summary>
    void Revoke(CapabilityTokenDigest digest);

    /// <summary>Marks every token bound to the order revoked (server-side revocation, CC-ORD-010).</summary>
    void RevokeAllForOrder(OrderId orderId);
}

/// <summary>In-memory <see cref="ICapabilityTokenStore"/> for tests and pre-persistence wiring.</summary>
public sealed class InMemoryCapabilityTokenStore : ICapabilityTokenStore
{
    private readonly ConcurrentDictionary<string, CapabilityTokenRecord> _records = new(StringComparer.Ordinal);

    public void Add(CapabilityTokenRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (!_records.TryAdd(record.Digest.Hex, record))
        {
            throw new InvalidOperationException("A token record with this digest already exists.");
        }
    }

    public CapabilityTokenRecord? Find(CapabilityTokenDigest digest) =>
        _records.TryGetValue(digest.Hex, out var record) ? record : null;

    public void Revoke(CapabilityTokenDigest digest)
    {
        if (_records.TryGetValue(digest.Hex, out var record))
        {
            _records[digest.Hex] = record with { Revoked = true };
        }
    }

    public void RevokeAllForOrder(OrderId orderId)
    {
        foreach (var entry in _records)
        {
            if (entry.Value.OrderId == orderId && !entry.Value.Revoked)
            {
                _records[entry.Key] = entry.Value with { Revoked = true };
            }
        }
    }
}
