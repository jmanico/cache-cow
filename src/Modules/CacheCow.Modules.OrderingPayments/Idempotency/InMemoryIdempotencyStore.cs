using System.Collections.Concurrent;

namespace CacheCow.Modules.OrderingPayments.Idempotency;

/// <summary>
/// In-memory <see cref="IIdempotencyStore"/> for tests and pre-persistence
/// wiring (the durable PostgreSQL store with a (scope, key) uniqueness
/// constraint is a persistence issue). Implements the port's atomic
/// reserve/claim contract: one winner per (scope, key); racing claimants with
/// a matching fingerprint block until the winner completes (replay) or
/// releases (they re-contend); fingerprint mismatches conflict immediately;
/// completed entries expire after the configured retention window.
/// </summary>
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<(IdempotencyScope Scope, string Key), Entry> _entries = new();
    private readonly IdempotencyOptions _options;
    private readonly TimeProvider _timeProvider;

    public InMemoryIdempotencyStore(IdempotencyOptions options, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public IdempotencyClaim Claim(IdempotencyScope scope, IdempotencyKey key, RequestFingerprint fingerprint)
    {
        var storeKey = (scope, key.Value);

        while (true)
        {
            var candidate = new Entry(fingerprint);
            var entry = _entries.GetOrAdd(storeKey, candidate);

            if (ReferenceEquals(entry, candidate))
            {
                return IdempotencyClaim.Winner();
            }

            // Retention window elapsed: the entry neither replays nor
            // conflicts; remove it and re-contend as a fresh key.
            if (IsExpired(entry))
            {
                _entries.TryRemove(new KeyValuePair<(IdempotencyScope, string), Entry>(storeKey, entry));
                continue;
            }

            // Same key, different request: conflict — whether in-flight or
            // completed, never the stored result, never fresh processing
            // (CC-SEC-015; SECURITY.md, Input validation rule 12).
            if (entry.Fingerprint != fingerprint)
            {
                return IdempotencyClaim.FingerprintConflict();
            }

            var outcome = entry.WaitForOutcome();
            if (outcome is not null)
            {
                return IdempotencyClaim.Replay(outcome);
            }

            // Winner released without a result (operation failed, no side
            // effect): the entry was removed; re-contend so a retry is not
            // poisoned (issue 037, AC-07).
        }
    }

    public void Complete(IdempotencyScope scope, IdempotencyKey key, object result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!_entries.TryGetValue((scope, key.Value), out var entry))
        {
            throw new InvalidOperationException(
                "Complete called without a live reservation for this (scope, key); Claim must win first (IIdempotencyStore contract).");
        }

        entry.Complete(result, _timeProvider.GetUtcNow());
    }

    public void Release(IdempotencyScope scope, IdempotencyKey key)
    {
        if (_entries.TryRemove((scope, key.Value), out var entry))
        {
            entry.ReleaseUnfulfilled();
        }
    }

    private bool IsExpired(Entry entry) =>
        entry.CompletedAt is { } completedAt
        && _timeProvider.GetUtcNow() - completedAt > _options.RetentionWindow;

    private sealed class Entry
    {
        private readonly TaskCompletionSource<object?> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Entry(RequestFingerprint fingerprint)
        {
            Fingerprint = fingerprint;
        }

        internal RequestFingerprint Fingerprint { get; }

        internal DateTimeOffset? CompletedAt { get; private set; }

        internal void Complete(object result, DateTimeOffset completedAt)
        {
            CompletedAt = completedAt;
            _completion.TrySetResult(result);
        }

        internal void ReleaseUnfulfilled() => _completion.TrySetResult(null);

        /// <summary>Blocks until the winner completes (returns the result) or releases (returns null).</summary>
        internal object? WaitForOutcome() => _completion.Task.GetAwaiter().GetResult();
    }
}
