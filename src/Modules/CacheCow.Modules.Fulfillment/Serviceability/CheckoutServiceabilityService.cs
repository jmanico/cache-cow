using CacheCow.Modules.Fulfillment.Routing;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.Fulfillment.Serviceability;

/// <summary>
/// Enforces the frozen-shipping constraints at checkout, server-side
/// (CC-FUL-002; ARCHITECTURE.md, "Server bounded contexts" item 5):
/// serviceable-postal-code validation per market, then the 48-hour maximum
/// carrier transit from the serving cold store (the routing origin,
/// CC-FUL-001). Evaluated at order submission from server-held data — a stale
/// client-cached "serviceable" result never overrides this check (issue 045,
/// AC-04/AC-07). Every unknown or unavailable input denies (fail closed).
/// </summary>
public sealed class CheckoutServiceabilityService
{
    private readonly IPostalCodeServiceabilitySource _postalCodes;
    private readonly IServingRegionSource _servingRegions;
    private readonly ITransitTimeEstimator _transitTimes;

    public CheckoutServiceabilityService(
        IPostalCodeServiceabilitySource postalCodes,
        IServingRegionSource servingRegions,
        ITransitTimeEstimator transitTimes)
    {
        ArgumentNullException.ThrowIfNull(postalCodes);
        ArgumentNullException.ThrowIfNull(servingRegions);
        ArgumentNullException.ThrowIfNull(transitTimes);
        _postalCodes = postalCodes;
        _servingRegions = servingRegions;
        _transitTimes = transitTimes;
    }

    /// <summary>
    /// Evaluates serviceability for a delivery in the transacting market. The
    /// postal code arrives validated and normalized per market (CC-ORD-002,
    /// issue 038; issue 045, AC-05); the market is the server-side transacting
    /// market, never a client hint (SECURITY.md, Authentication rule 10).
    /// </summary>
    public ServiceabilityDecision Evaluate(Market market, PostalCode postalCode)
    {
        try
        {
            if (!_postalCodes.IsServiceable(market, postalCode))
            {
                return ServiceabilityDecision.Denied(ServiceabilityDenialReason.PostalCodeNotServiceable);
            }

            var servingStore = _servingRegions.ServesMarket(market)
                ? _servingRegions.FindServingStore(market, postalCode)
                : null;
            if (servingStore is null)
            {
                return ServiceabilityDecision.Denied(ServiceabilityDenialReason.NoServingColdStore);
            }

            var transit = _transitTimes.EstimateTransit(servingStore.Value, market, postalCode);
            if (transit is null)
            {
                return ServiceabilityDecision.Denied(ServiceabilityDenialReason.TransitEstimateUnavailable);
            }

            return FrozenTransitConstraint.IsWithinLimit(transit.Value)
                ? ServiceabilityDecision.Serviceable(servingStore.Value, transit.Value)
                : ServiceabilityDecision.Denied(ServiceabilityDenialReason.TransitExceedsFrozenLimit);
        }
        catch (Exception)
        {
            // Fail closed: if the evaluation (including the carrier dependency)
            // errors, the order is rejected, never optimistically accepted
            // (issue 045, Failure Behavior; SECURITY.md, Logging rule 2).
            return ServiceabilityDecision.Denied(ServiceabilityDenialReason.EvaluationFailed);
        }
    }
}
