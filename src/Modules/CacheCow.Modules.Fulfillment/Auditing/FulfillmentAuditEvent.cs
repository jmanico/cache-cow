namespace CacheCow.Modules.Fulfillment.Auditing;

/// <summary>
/// An audit event for a privileged fulfillment action, carrying the fields the
/// audit mandate requires: actor, action, object, before/after, timestamp
/// (CC-DSH-004; SECURITY.md, Logging rule 6). The append-only store itself is
/// the Back Office context's (issue 081); this context only emits events
/// through <see cref="IFulfillmentAuditSink"/>.
/// </summary>
public sealed record FulfillmentAuditEvent
{
    /// <summary>The action recorded for a cross-region fulfillment override (CC-FUL-001).</summary>
    public const string CrossRegionOverrideAction = "fulfillment.order.cross-region-override";

    private FulfillmentAuditEvent(
        string action,
        string actorId,
        OrderReference order,
        ColdStoreId fromStore,
        ColdStoreId toStore,
        DateTimeOffset occurredAt)
    {
        Action = action;
        ActorId = actorId;
        Order = order;
        FromStore = fromStore;
        ToStore = toStore;
        OccurredAt = occurredAt;
    }

    public string Action { get; }

    /// <summary>The staff actor, taken from the override proof — never from client input (SECURITY.md, Input validation rule 3).</summary>
    public string ActorId { get; }

    public OrderReference Order { get; }

    /// <summary>Cold-store assignment before the action.</summary>
    public ColdStoreId FromStore { get; }

    /// <summary>Cold-store assignment after the action.</summary>
    public ColdStoreId ToStore { get; }

    public DateTimeOffset OccurredAt { get; }

    public static FulfillmentAuditEvent CrossRegionOverride(
        string actorId,
        OrderReference order,
        ColdStoreId fromStore,
        ColdStoreId toStore,
        DateTimeOffset occurredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        return new FulfillmentAuditEvent(CrossRegionOverrideAction, actorId, order, fromStore, toStore, occurredAt);
    }
}
