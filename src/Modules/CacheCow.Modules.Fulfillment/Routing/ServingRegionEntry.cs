using CacheCow.SharedKernel;

namespace CacheCow.Modules.Fulfillment.Routing;

/// <summary>
/// One row of serving-region data: in <paramref name="Market"/>, deliveries to
/// <paramref name="PostalCode"/> are served by <paramref name="Store"/>
/// (CC-FUL-001). A store may appear in many rows and many markets — a regional
/// cold store holds frozen inventory for one or more markets (REQUIREMENTS.md §2).
/// </summary>
public sealed record ServingRegionEntry(Market Market, PostalCode PostalCode, ColdStoreId Store);
