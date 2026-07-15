namespace CacheCow.Modules.WholesaleB2B.Partners;

/// <summary>
/// Port toward the append-only audit store (SECURITY.md, Logging rule 6;
/// ARCHITECTURE.md, Dependency rule 6). The durable store itself — INSERT-only
/// database privileges, WORM retention (CC-DSH-004, CC-SEC-020) — is issue 081;
/// this module only emits.
///
/// Contract: <see cref="Append"/> MUST persist the event durably before
/// returning, and MUST throw on any failure. Callers treat a throw as a denial
/// of the action being audited — a tenancy transition never commits without its
/// audit record (issue 049, AC-02; SECURITY.md, Logging rule 2). Appended
/// events are never mutated or deleted; corrections are new records.
/// </summary>
public interface IPartnerAuditSink
{
    /// <summary>Durably appends one audit event, or throws (fail closed).</summary>
    void Append(PartnerAuditEvent auditEvent);
}
