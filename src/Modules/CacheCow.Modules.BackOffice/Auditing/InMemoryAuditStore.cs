using System.Collections.ObjectModel;

namespace CacheCow.Modules.BackOffice.Auditing;

/// <summary>
/// Thread-safe in-memory audit store behind <see cref="IAuditStore"/> until
/// the durable PostgreSQL store lands (issue 015 provisions the INSERT-only
/// roles; persistence is additionally AT RISK on the open residency
/// decisions — issue 081 banner; ARCHITECTURE.md, "Known unknowns").
///
/// The append-only property holds through the public surface: the only
/// mutating member is <see cref="Append"/>, the internal list never escapes,
/// and every query returns a read-only snapshot copy detached from internal
/// state — no public path mutates or removes a retained event (CC-SEC-020;
/// verified by reflection and snapshot tests). Events themselves are
/// immutable records.
///
/// Replication: each event is delivered to the <see cref="IWormReplicationSink"/>
/// AFTER being durably retained, so a sink failure never loses the event —
/// the retained record remains the source for at-least-once re-delivery
/// (issue 081, Failure Behavior). A sink exception propagates to the caller
/// (surfacing the failure for alerting) with the event already retained.
/// </summary>
public sealed class InMemoryAuditStore : IAuditStore
{
    private readonly Lock gate = new();
    private readonly List<AuditEvent> events = [];
    private readonly IWormReplicationSink wormReplicationSink;

    public InMemoryAuditStore(IWormReplicationSink wormReplicationSink)
    {
        ArgumentNullException.ThrowIfNull(wormReplicationSink);
        this.wormReplicationSink = wormReplicationSink;
    }

    /// <inheritdoc />
    public void Append(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        lock (gate)
        {
            events.Add(auditEvent);
        }

        // After retention, never instead of it: the event above survives any
        // sink failure and remains available for re-replication.
        wormReplicationSink.Replicate(auditEvent);
    }

    /// <inheritdoc />
    public IReadOnlyList<AuditEvent> Query(AuditQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        AuditEvent[] snapshot;
        lock (gate)
        {
            snapshot = events.Where(query.Matches).ToArray();
        }

        return new ReadOnlyCollection<AuditEvent>(snapshot);
    }
}
