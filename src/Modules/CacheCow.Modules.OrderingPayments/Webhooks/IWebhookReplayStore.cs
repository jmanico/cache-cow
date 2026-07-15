using System.Collections.Concurrent;

namespace CacheCow.Modules.OrderingPayments.Webhooks;

/// <summary>
/// Port recording seen webhook event identifiers (nonces) for replay
/// rejection (issue 041; CC-SEC-014: timestamp/nonce replay bounds).
///
/// Contract:
/// - <see cref="TryRegister"/> is atomic: the first registration of a
///   (processor, event id) pair returns true; every subsequent registration
///   within the retention period returns false (replay).
/// - Entries are scoped per processor — one processor's event id never
///   collides with another's.
/// - The verifier registers an id only AFTER signature and timestamp checks
///   pass, so a forged delivery can never poison the store and block the
///   authentic delivery of the same event.
/// - Any store failure throws; the verifier fails closed (SECURITY.md,
///   Logging rule 2). The retention period for seen ids is an open question
///   (issue 041, Open Questions).
/// </summary>
public interface IWebhookReplayStore
{
    /// <summary>True if this (processor, event id) was not seen before and is now recorded; false on replay.</summary>
    bool TryRegister(string processorName, string eventId, DateTimeOffset seenAt);
}

/// <summary>
/// In-memory <see cref="IWebhookReplayStore"/> for tests and pre-persistence
/// wiring (the durable store lives with the persistence issues). Entries are
/// pruned once they are older than twice the configured max event age —
/// anything older is already rejected by the timestamp bound, so the id no
/// longer needs to be remembered.
/// </summary>
public sealed class InMemoryWebhookReplayStore : IWebhookReplayStore
{
    private readonly ConcurrentDictionary<(string Processor, string EventId), DateTimeOffset> _seen = new();
    private readonly WebhookVerificationOptions _options;

    public InMemoryWebhookReplayStore(WebhookVerificationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public bool TryRegister(string processorName, string eventId, DateTimeOffset seenAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

        Prune(seenAt);
        return _seen.TryAdd((processorName, eventId), seenAt);
    }

    private void Prune(DateTimeOffset now)
    {
        var cutoff = now - _options.MaxEventAge - _options.MaxEventAge;
        foreach (var entry in _seen)
        {
            if (entry.Value < cutoff)
            {
                _seen.TryRemove(entry);
            }
        }
    }
}
