namespace CacheCow.Modules.BackOffice.Auditing;

/// <summary>
/// Port toward retention-locked (WORM) replica storage for the audit stream
/// (CC-SEC-020; SECURITY.md, Logging rule 6): the second, independent control
/// so no single compromised application or DBA credential can both write and
/// erase history. Financial-class events carry the ratified 7-year window
/// (<see cref="AuditRetention.RatifiedFinancialRetentionYears"/>).
///
/// OPEN DECISIONS (issue 081, Open Questions — human decision required, not
/// resolved here): the WORM storage service is not named in the specs (no
/// confirmed Azure service is designated), and the replica's residency zone
/// is entangled with the open "Telemetry &amp; backup residency" decision
/// (ARCHITECTURE.md, "Known unknowns"). This port models the contract only.
///
/// Delivery semantics: AT-LEAST-ONCE. The store invokes
/// <see cref="Replicate"/> for every appended event after durably retaining
/// it, and a retained event may be re-delivered (e.g. re-replication after a
/// failure), so implementations MUST deduplicate by
/// <see cref="AuditEvent.EventId"/>. Implementations SHOULD absorb transient
/// failures internally (queue and retry) rather than throw; replication
/// failure or lag never un-retains the primary event and is monitored and
/// alerted, not silently ignored (issue 081, Failure Behavior; SECURITY.md,
/// Logging rule 3). No credential holding write access here may also hold
/// retention-lock control (SECURITY.md, Logging rule 6).
/// </summary>
public interface IWormReplicationSink
{
    /// <summary>Delivers one already-retained audit event toward WORM storage (at-least-once; dedupe by <see cref="AuditEvent.EventId"/>).</summary>
    void Replicate(AuditEvent auditEvent);
}

/// <summary>
/// Default sink until the WORM storage service and its residency zone are
/// decided by a human (issue 081, Open Questions; ARCHITECTURE.md, "Known
/// unknowns"): a no-op representing unbounded replication lag. Events remain
/// durably retained in the primary store for later at-least-once
/// re-replication, so nothing is lost — but production MUST alert on
/// replication lag (issue 081, Alerting), and provisioning the real target is
/// blocked on the open decisions, deliberately not guessed here (CLAUDE.md,
/// working rules).
/// </summary>
public sealed class UnconfiguredWormReplicationSink : IWormReplicationSink
{
    public void Replicate(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        // Intentionally nothing: no replication target exists yet. The event
        // stays in the primary store, available for re-replication once the
        // WORM service decision lands.
    }
}
