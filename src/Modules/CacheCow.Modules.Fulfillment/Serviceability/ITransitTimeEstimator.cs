using CacheCow.SharedKernel;

namespace CacheCow.Modules.Fulfillment.Serviceability;

/// <summary>
/// Port for carrier transit-time estimates from a regional cold store to a
/// delivery postal code. The production adapter is the EasyPost multi-carrier
/// aggregator (ARCHITECTURE.md, Carriers — a later issue); this context only
/// compares the answer against <see cref="FrozenTransitConstraint"/>
/// (CC-FUL-002). Carrier choice is never hardcoded here (issue 045,
/// Anti-Patterns).
/// </summary>
public interface ITransitTimeEstimator
{
    /// <summary>
    /// The best available carrier transit time from the origin store to the
    /// destination postal code, or null when no estimate is available.
    /// Callers fail closed on null (issue 045, Failure Behavior).
    /// </summary>
    TimeSpan? EstimateTransit(ColdStoreId originStore, Market market, PostalCode destinationPostalCode);
}
