namespace CacheCow.Modules.Fulfillment.Auditing;

/// <summary>
/// Fail-closed default until the host wires the append-only audit store
/// (issue 081): every append reports failure, so every action that requires an
/// audit trail is denied (issue 044, AC-07; SECURITY.md, Logging rule 2). This
/// is deliberately not a no-op success — a sink that swallows events would let
/// unaudited cross-region fulfillment exist.
/// </summary>
public sealed class UnconfiguredFulfillmentAuditSink : IFulfillmentAuditSink
{
    public bool TryAppend(FulfillmentAuditEvent auditEvent) => false;
}
