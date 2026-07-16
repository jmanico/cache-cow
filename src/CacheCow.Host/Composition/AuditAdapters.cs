using System.Diagnostics;
using CacheCow.Modules.BackOffice.Auditing;
using CacheCow.Modules.Fulfillment.Auditing;
using CacheCow.Modules.OrderingPayments.Orders;
using CacheCow.Modules.WholesaleB2B.Partners;

namespace CacheCow.Host.Composition;

/// <summary>
/// Host adapters from the module-local audit emission ports — OrderingPayments
/// <see cref="IAuditSink"/>, Fulfillment <see cref="IFulfillmentAuditSink"/>,
/// WholesaleB2B <see cref="IPartnerAuditSink"/> — onto the BackOffice
/// append-only audit store's write-only face (<see cref="IAuditEventSink"/>),
/// exactly as each port's XML contract directs (CC-DSH-004, CC-ORD-006;
/// SECURITY.md, Logging rule 6; ARCHITECTURE.md, Dependency rule 9: adapters
/// are host wiring — modules never reference each other).
///
/// Every adapter maps fields faithfully — actor, action, object, before/after,
/// server timestamp — and preserves the append-before-effect contract: a
/// failed append propagates (or reports false, per the port's own contract) so
/// the audited action is denied, never performed unaudited (SECURITY.md,
/// Logging rule 2).
/// </summary>
internal static class AuditCorrelation
{
    /// <summary>
    /// Correlation id linking the audit record to the request's structured
    /// logs (SECURITY.md, Logging rule 1): the current W3C activity id — the
    /// same value the RFC 9457 problem-details pipeline stamps as
    /// correlationId — or an explicit "unlinked" marker when no activity is
    /// current (e.g. a background job), never an empty field.
    /// </summary>
    internal static string CurrentCorrelationId() =>
        Activity.Current?.Id ?? "unlinked:" + Guid.NewGuid().ToString("D");
}

/// <summary>
/// OrderingPayments <see cref="IAuditSink"/> → BackOffice
/// <see cref="IAuditEventSink"/>. Order state transitions belong to the
/// financial audit stream (CC-ORD-006 transitions include refunds), so they
/// carry the ratified 7-year <see cref="AuditRetentionClass.Financial"/> class
/// (CC-DSH-004). <see cref="OrderAuditEvent"/> carries no RBAC role — the
/// state machine is driven by system events as well as staff — so the role
/// field is recorded as the explicit marker <see cref="UnspecifiedActorRole"/>
/// rather than a fabricated role (flagged as a port-shape gap, not resolved
/// here).
/// </summary>
internal sealed class OrderingPaymentsAuditSinkAdapter : IAuditSink
{
    internal const string ObjectType = "order";
    internal const string UnspecifiedActorRole = "(unspecified)";

    private readonly IAuditEventSink _store;

    public OrderingPaymentsAuditSinkAdapter(IAuditEventSink store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <inheritdoc />
    public void Append(OrderAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        // AuditEvent.Create validates every field and throws on anything
        // invalid; the throw propagates so the transition is denied
        // (append-before-effect, SECURITY.md Logging rule 2).
        _store.Append(AuditEvent.Create(
            auditEvent.Actor,
            UnspecifiedActorRole,
            auditEvent.Action,
            ObjectType,
            auditEvent.OrderId.ToString(),
            auditEvent.FromState.ToString(),
            auditEvent.ToState.ToString(),
            auditEvent.Timestamp,
            AuditCorrelation.CurrentCorrelationId(),
            AuditRetentionClass.Financial));
    }
}

/// <summary>
/// Fulfillment <see cref="IFulfillmentAuditSink"/> → BackOffice
/// <see cref="IAuditEventSink"/> (CC-FUL-001: cross-region override is a
/// privileged, audited dashboard action). The port contract is boolean:
/// false on any failure, and the caller denies the action — so every failure
/// path here reports false rather than leaking an exception past the port
/// shape. Cross-region overrides are operational (not financial) actions:
/// <see cref="AuditRetentionClass.Standard"/>.
/// </summary>
internal sealed class FulfillmentAuditSinkAdapter : IFulfillmentAuditSink
{
    internal const string ObjectType = "order";

    private readonly IAuditEventSink _store;

    public FulfillmentAuditSinkAdapter(IAuditEventSink store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <inheritdoc />
    public bool TryAppend(FulfillmentAuditEvent auditEvent)
    {
        if (auditEvent is null)
        {
            return false;
        }

        try
        {
            _store.Append(AuditEvent.Create(
                auditEvent.ActorId,
                OrderingPaymentsAuditSinkAdapter.UnspecifiedActorRole,
                auditEvent.Action,
                ObjectType,
                auditEvent.Order.Value,
                auditEvent.FromStore.Value,
                auditEvent.ToStore.Value,
                auditEvent.OccurredAt,
                AuditCorrelation.CurrentCorrelationId(),
                AuditRetentionClass.Standard));
            return true;
        }
#pragma warning disable CA1031 // Fail closed per the port contract: any append failure reports false and the caller denies the action (SECURITY.md, Logging rule 2).
        catch (Exception)
#pragma warning restore CA1031
        {
            return false;
        }
    }
}

/// <summary>
/// WholesaleB2B <see cref="IPartnerAuditSink"/> → BackOffice
/// <see cref="IAuditEventSink"/> (CC-WHS-002: tenancy transitions are audited
/// append-before-effect). The partner event carries the full field set
/// including the authorizing RBAC role; failures propagate per the port
/// contract so the tenancy transition is denied. Onboarding transitions are
/// operational actions: <see cref="AuditRetentionClass.Standard"/>.
/// </summary>
internal sealed class PartnerAuditSinkAdapter : IPartnerAuditSink
{
    internal const string ObjectType = "partner";

    private readonly IAuditEventSink _store;

    public PartnerAuditSinkAdapter(IAuditEventSink store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <inheritdoc />
    public void Append(PartnerAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        _store.Append(AuditEvent.Create(
            auditEvent.Actor,
            auditEvent.ActorRole,
            auditEvent.Action,
            ObjectType,
            auditEvent.PartnerId.Value,
            auditEvent.FromState.ToString(),
            auditEvent.ToState.ToString(),
            auditEvent.Timestamp,
            AuditCorrelation.CurrentCorrelationId(),
            AuditRetentionClass.Standard));
    }
}
