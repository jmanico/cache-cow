namespace CacheCow.Modules.Fulfillment.Auditing;

/// <summary>
/// Port to the append-only audit store (CC-DSH-004; SECURITY.md, Logging
/// rule 6; issue 081 owns the store, the host wires the adapter). Callers MUST
/// append before the audited effect takes place and MUST treat a failed append
/// as denial of the action — an unaudited cross-region fulfillment must not
/// exist (issue 044, AC-07; SECURITY.md, Logging rule 2).
/// </summary>
public interface IFulfillmentAuditSink
{
    /// <summary>
    /// Appends the event to the audit store. Returns false (or throws) when the
    /// event could not be durably appended; the caller then denies the action.
    /// </summary>
    bool TryAppend(FulfillmentAuditEvent auditEvent);
}
