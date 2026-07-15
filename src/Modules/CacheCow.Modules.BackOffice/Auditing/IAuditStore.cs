namespace CacheCow.Modules.BackOffice.Auditing;

/// <summary>
/// The append-only audit store of the Back Office bounded context
/// (CC-DSH-004; CC-ORD-006; CC-SEC-020; SECURITY.md, Logging rule 6;
/// ARCHITECTURE.md, "Server bounded contexts" 8 and Dependency rule 6).
///
/// The ONLY write operation on this surface is the inherited
/// <see cref="IAuditEventSink.Append"/>: no update, delete, truncate, or
/// clear member exists anywhere on the contract, making mutation of history
/// unrepresentable at the type level (verified by reflection tests). This is
/// the code-level face of the same property the database enforces by
/// privilege — INSERT-only application roles, no UPDATE/DELETE/TRUNCATE —
/// which is the real control, provisioned in issue 015 (CC-SEC-020:
/// append-only by privilege, not convention).
///
/// Reads are query-only, filtered by actor, object, and time
/// (<see cref="AuditQuery"/>), and reach staff only through role-gated
/// dashboard views (issue 080). Corrections to audited facts are new
/// compensating events, never mutations (issue 081, AC-07).
/// </summary>
public interface IAuditStore : IAuditEventSink
{
    /// <summary>
    /// Returns the events matching the filter, in append order, as a
    /// read-only snapshot detached from the store's internal state.
    /// </summary>
    IReadOnlyList<AuditEvent> Query(AuditQuery query);
}
