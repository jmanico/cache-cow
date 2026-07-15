using CacheCow.SharedKernel;

namespace CacheCow.Modules.Fulfillment.Serviceability;

/// <summary>
/// Fail-closed default until the EasyPost adapter lands (later issue;
/// ARCHITECTURE.md, Carriers): no estimate is ever available, so no order
/// passes the transit check — rejection, never optimistic acceptance
/// (issue 045, Failure Behavior; SECURITY.md, Logging rule 2).
/// </summary>
public sealed class UnavailableTransitTimeEstimator : ITransitTimeEstimator
{
    public TimeSpan? EstimateTransit(ColdStoreId originStore, Market market, PostalCode destinationPostalCode) => null;
}
