namespace CacheCow.Modules.BackOffice.Auditing;

/// <summary>
/// The write-only target surface of the append-only audit store (CC-DSH-004;
/// SECURITY.md, Logging rule 6). This is the adapter target for the
/// module-local audit ports of other bounded contexts — OrderingPayments
/// <c>IAuditSink</c>, Fulfillment <c>IFulfillmentAuditSink</c>, WholesaleB2B
/// <c>IPartnerAuditSink</c>: the host wires adapters from those ports onto
/// this interface (adapters are host wiring, not this module's; modules
/// reference only the shared kernel, ARCHITECTURE.md, Dependency rule 9).
/// Writers get exactly this surface — no query access (least privilege).
///
/// Contract (append-before-effect, shared with every emitting port):
/// <see cref="Append"/> MUST durably retain the event before returning and
/// MUST throw on any failure. Callers append BEFORE committing the audited
/// effect and treat a throw as denial of the action — no covered privileged
/// action or state transition completes without its audit record (issue 081,
/// AC-01 and Failure Behavior; SECURITY.md, Logging rule 2). Appended events
/// are never mutated or deleted; corrections are new compensating events
/// (ARCHITECTURE.md, Dependency rule 6).
/// </summary>
public interface IAuditEventSink
{
    /// <summary>Durably appends one audit event, or throws (fail closed).</summary>
    void Append(AuditEvent auditEvent);
}
