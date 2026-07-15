namespace CacheCow.Modules.OrderingPayments.Orders;

/// <summary>
/// The audit record emitted for every order state transition (CC-ORD-006;
/// SECURITY.md, Logging rule 6): actor, action, object (order id),
/// before/after state, timestamp. The shape is deliberately closed — no
/// free-form payload field exists, so credentials, tokens, PANs, or PII cannot
/// ride along (SECURITY.md, Logging rule 4). The timestamp is server-set by
/// <see cref="OrderStateMachine"/>, never caller-supplied (SECURITY.md,
/// Input validation rule 3).
/// </summary>
public sealed record OrderAuditEvent(
    string Actor,
    string Action,
    OrderId OrderId,
    OrderState FromState,
    OrderState ToState,
    DateTimeOffset Timestamp);
