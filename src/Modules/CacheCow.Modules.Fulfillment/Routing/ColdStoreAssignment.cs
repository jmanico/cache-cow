namespace CacheCow.Modules.Fulfillment.Routing;

/// <summary>
/// A routed order's cold-store assignment. <see cref="ServingStore"/> is the
/// store the serving-region data selected for the delivery address;
/// <see cref="AssignedStore"/> is where the order will actually be fulfilled.
/// They differ only after an audited cross-region override (CC-FUL-001) —
/// <see cref="OrderRoutingService.RouteOrder"/> always produces them equal.
/// </summary>
public sealed record ColdStoreAssignment(OrderReference Order, ColdStoreId ServingStore, ColdStoreId AssignedStore)
{
    /// <summary>True when the order is fulfilled outside its serving region.</summary>
    public bool IsCrossRegion => ServingStore != AssignedStore;
}
