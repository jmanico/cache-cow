using CacheCow.Modules.Fulfillment.Auditing;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.Fulfillment.Routing;

/// <summary>
/// Routes every consumer order to the regional cold store serving its delivery
/// address, and applies cross-region overrides only with an explicit,
/// audited operations authorization (CC-FUL-001; ARCHITECTURE.md, "Server
/// bounded contexts" item 5). By construction <see cref="RouteOrder"/> can
/// only assign the serving store — the sole cross-region path is
/// <see cref="ApplyCrossRegionOverride"/>, which requires an
/// <see cref="OverrideAuthorization"/> proof and a successful audit append before
/// the override takes effect (issue 044, AC-02/AC-03/AC-07).
/// </summary>
public sealed class OrderRoutingService
{
    private readonly IServingRegionSource _servingRegions;
    private readonly IFulfillmentAuditSink _auditSink;
    private readonly TimeProvider _timeProvider;

    public OrderRoutingService(
        IServingRegionSource servingRegions,
        IFulfillmentAuditSink auditSink,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(servingRegions);
        ArgumentNullException.ThrowIfNull(auditSink);
        _servingRegions = servingRegions;
        _auditSink = auditSink;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Resolves the regional cold store serving the delivery address and
    /// assigns the order to it (issue 044, AC-01). The delivery postal code
    /// arrives validated and normalized from address capture (CC-ORD-002,
    /// issue 038); no client routing hint exists on this API (SECURITY.md,
    /// Authentication rule 10). Unknown market or postal code fails closed
    /// with a typed reason — never a default store (issue 044, AC-06).
    /// </summary>
    public RoutingResult RouteOrder(OrderReference order, Market market, PostalCode postalCode)
    {
        try
        {
            if (!_servingRegions.ServesMarket(market))
            {
                return RoutingResult.Failed(RoutingFailureReason.MarketNotServed);
            }

            var servingStore = _servingRegions.FindServingStore(market, postalCode);
            return servingStore is null
                ? RoutingResult.Failed(RoutingFailureReason.PostalCodeNotServed)
                : RoutingResult.Routed(new ColdStoreAssignment(order, servingStore.Value, servingStore.Value));
        }
        catch (Exception)
        {
            // Fail closed: an exception in a routing/gating path is a denial,
            // never a bypass or a fallback assignment (SECURITY.md, Logging
            // rule 2; issue 044, AC-06).
            return RoutingResult.Failed(RoutingFailureReason.ResolutionFailed);
        }
    }

    /// <summary>
    /// Re-routes an order to another regional cold store. Requires the typed
    /// <paramref name="permission"/> proof issued by the dashboard context —
    /// there is no overload without it — and appends the audit event before
    /// the override takes effect: if the append fails, the override is denied,
    /// so an unaudited cross-region fulfillment cannot exist (CC-FUL-001;
    /// CC-DSH-004; issue 044, AC-03/AC-07; SECURITY.md, Logging rules 2 and 6).
    /// </summary>
    public OverrideResult ApplyCrossRegionOverride(
        ColdStoreAssignment current,
        ColdStoreId targetStore,
        OverrideAuthorization permission)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(permission);

        try
        {
            if (!_servingRegions.IsKnownStore(targetStore))
            {
                return OverrideResult.Denied(OverrideDenialReason.UnknownTargetStore);
            }

            var auditEvent = FulfillmentAuditEvent.CrossRegionOverride(
                permission.ActorId,
                current.Order,
                fromStore: current.AssignedStore,
                toStore: targetStore,
                occurredAt: _timeProvider.GetUtcNow());

            bool appended;
            try
            {
                appended = _auditSink.TryAppend(auditEvent);
            }
            catch (Exception)
            {
                appended = false;
            }

            return appended
                ? OverrideResult.Applied(current with { AssignedStore = targetStore })
                : OverrideResult.Denied(OverrideDenialReason.AuditAppendFailed);
        }
        catch (Exception)
        {
            // Fail closed (SECURITY.md, Logging rule 2).
            return OverrideResult.Denied(OverrideDenialReason.EvaluationFailed);
        }
    }
}
