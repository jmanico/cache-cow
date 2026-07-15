using System.Collections.Concurrent;

namespace CacheCow.Modules.Invoicing.Numbering;

/// <summary>
/// In-memory <see cref="ILegalEntitySequence"/> for tests and local
/// composition: strictly gapless per (legal entity, series), atomic under
/// concurrency via <see cref="Interlocked.Increment(ref long)"/> on a
/// per-key counter (issue 046, AC-01). Not a production adapter — durable,
/// transactional allocation awaits the persistence topology decision
/// (ARCHITECTURE.md, "Known unknowns": data residency vs. single primary
/// write region).
/// </summary>
public sealed class InMemoryLegalEntitySequence : ILegalEntitySequence
{
    private sealed class Counter
    {
        private long _value;

        public long Increment() => Interlocked.Increment(ref _value);
    }

    private readonly ConcurrentDictionary<(LegalEntityId Entity, DocumentSeries Series), Counter> _counters = new();

    public long AllocateNext(LegalEntityId legalEntity, DocumentSeries series)
    {
        // Touch .Value so an uninitialized (default) LegalEntityId fails closed
        // instead of silently keying a shared sequence.
        _ = legalEntity.Value;

        var counter = _counters.GetOrAdd((legalEntity, series), static _ => new Counter());
        return counter.Increment();
    }
}
