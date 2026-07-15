using System.Collections.Concurrent;

namespace CacheCow.Modules.IdentityAccess.EmailOtp;

/// <summary>
/// In-memory <see cref="IOtpStore"/> for tests and pre-persistence wiring.
/// NOT the durable store the multi-region topology ultimately requires (that
/// implementation is a later persistence issue and is entangled with the open
/// residency/write-region decision — ARCHITECTURE.md, "Known unknowns").
/// Holds digests only; plaintext codes never reach it.
/// </summary>
public sealed class InMemoryOtpStore : IOtpStore
{
    private readonly ConcurrentDictionary<string, OtpRecord> _records = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lockouts = new();

    public void Put(OtpRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        // Unconditional replace: a newer code invalidates any older one (AC-03).
        _records[record.AccountKey] = record;
    }

    public OtpRecord? FindCurrent(string accountKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountKey);
        return _records.TryGetValue(accountKey, out var record) ? record : null;
    }

    public void Remove(string accountKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountKey);
        _records.TryRemove(accountKey, out _);
    }

    public DateTimeOffset? FindLockoutExpiry(string accountKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountKey);
        return _lockouts.TryGetValue(accountKey, out var expiry) ? expiry : null;
    }

    public void SetLockoutExpiry(string accountKey, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountKey);
        _lockouts[accountKey] = expiresAt;
    }
}
