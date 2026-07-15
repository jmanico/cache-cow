using CacheCow.Modules.Fulfillment.Routing;
using CacheCow.Modules.Fulfillment.Serviceability;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.Fulfillment.Tests;

/// <summary>
/// Issue 045 (CC-FUL-002): checkout serviceability enforces the per-market
/// serviceable-postal-code sets and the 48-hour frozen transit maximum
/// (ratified 2026-07-15), failing closed on every unknown or unavailable
/// input, with a machine-readable decision for checkout.
/// </summary>
public sealed class CheckoutServiceabilityTests
{
    private static CheckoutServiceabilityService CreateService(
        StubTransitTimeEstimator? transit = null,
        IServingRegionSource? servingRegions = null,
        IPostalCodeServiceabilitySource? postalCodes = null) =>
        new(
            postalCodes ?? FulfillmentTestData.ServiceablePostalCodes(),
            servingRegions ?? FulfillmentTestData.ServingRegions(),
            transit ?? new StubTransitTimeEstimator { Result = TimeSpan.FromHours(24) });

    [Theory]
    [Requirement("CC-FUL-002")]
    [InlineData("US", "10001", "cs-us-east")]
    [InlineData("US", "94103", "cs-us-west")]
    [InlineData("ES", "28001", "cs-es-madrid")]
    [InlineData("MX", "06000", "cs-mx-central")]
    [InlineData("DE", "10115", "cs-de-central")]
    [InlineData("JP", "100-0001", "cs-jp-kanto")]
    [InlineData("IN", "560001", "cs-in-south")]
    public void Serviceable_postal_code_with_transit_within_limit_is_allowed(
        string marketCode, string postalCode, string expectedOrigin)
    {
        var transit = new StubTransitTimeEstimator { Result = TimeSpan.FromHours(24) };

        var decision = CreateService(transit).Evaluate(Market.Parse(marketCode), PostalCode.Parse(postalCode));

        Assert.True(decision.IsServiceable);
        Assert.Equal(ServiceabilityDenialReason.None, decision.Denial);
        Assert.Equal("serviceable", decision.Code);
        Assert.Equal(ColdStoreId.Parse(expectedOrigin), decision.ServingStore);
        Assert.Equal(ColdStoreId.Parse(expectedOrigin), transit.LastOrigin);
        Assert.Equal(TimeSpan.FromHours(24), decision.EstimatedTransit);
    }

    [Theory]
    [Requirement("CC-FUL-002")]
    [InlineData("US", "00000")]
    [InlineData("DE", "80331")]
    [InlineData("JP", "550-0001")]
    [InlineData("IN", "110001")]
    public void Postal_code_not_on_the_markets_serviceable_set_is_denied(string marketCode, string postalCode)
    {
        var decision = CreateService().Evaluate(Market.Parse(marketCode), PostalCode.Parse(postalCode));

        Assert.False(decision.IsServiceable);
        Assert.Equal(ServiceabilityDenialReason.PostalCodeNotServiceable, decision.Denial);
        Assert.Equal("postal-code-not-serviceable", decision.Code);
        Assert.Null(decision.ServingStore);
        Assert.Null(decision.EstimatedTransit);
    }

    [Fact]
    [Requirement("CC-FUL-002")]
    public void Serviceable_postal_code_is_market_scoped_not_global()
    {
        // 10115 is serviceable in DE; the same string in the US market is not.
        var decision = CreateService().Evaluate(Market.US, PostalCode.Parse("10115"));

        Assert.False(decision.IsServiceable);
        Assert.Equal(ServiceabilityDenialReason.PostalCodeNotServiceable, decision.Denial);
    }

    [Fact]
    [Requirement("CC-FUL-002")]
    [Requirement("CC-FUL-001")]
    public void Serviceable_postal_code_without_a_serving_cold_store_is_denied()
    {
        // 73301 is on the US serviceable set but no store serves it — there is
        // no transit origin, so the decision fails closed.
        var decision = CreateService().Evaluate(Market.US, PostalCode.Parse("73301"));

        Assert.False(decision.IsServiceable);
        Assert.Equal(ServiceabilityDenialReason.NoServingColdStore, decision.Denial);
        Assert.Equal("no-serving-cold-store", decision.Code);
    }

    [Theory]
    [Requirement("CC-FUL-002")]
    [InlineData(47, 59, true)]  // 47h59m: within the limit
    [InlineData(48, 0, true)]   // exactly the 48h maximum: within the limit
    [InlineData(48, 1, false)]  // 48h01m: beyond the limit
    public void Transit_limit_boundary_is_exactly_48_hours(int hours, int minutes, bool expectServiceable)
    {
        var transit = new StubTransitTimeEstimator { Result = new TimeSpan(hours, minutes, 0) };

        var decision = CreateService(transit).Evaluate(Market.US, PostalCode.Parse("10001"));

        Assert.Equal(expectServiceable, decision.IsServiceable);
        if (!expectServiceable)
        {
            Assert.Equal(ServiceabilityDenialReason.TransitExceedsFrozenLimit, decision.Denial);
            Assert.Equal("transit-exceeds-frozen-limit", decision.Code);
            Assert.Null(decision.EstimatedTransit);
        }
    }

    [Fact]
    [Requirement("CC-FUL-002")]
    public void The_ratified_constant_is_48_hours()
    {
        Assert.Equal(TimeSpan.FromHours(48), FrozenTransitConstraint.MaximumTransit);
    }

    [Fact]
    [Requirement("CC-FUL-002")]
    public void Unavailable_transit_estimate_is_denied_fail_closed()
    {
        var transit = new StubTransitTimeEstimator { Result = null };

        var decision = CreateService(transit).Evaluate(Market.US, PostalCode.Parse("10001"));

        Assert.False(decision.IsServiceable);
        Assert.Equal(ServiceabilityDenialReason.TransitEstimateUnavailable, decision.Denial);
        Assert.Equal("transit-estimate-unavailable", decision.Code);
    }

    [Fact]
    [Requirement("CC-FUL-002")]
    public void Estimator_failure_is_a_denial_never_an_acceptance()
    {
        var transit = new StubTransitTimeEstimator { ThrowOnEstimate = true };

        var decision = CreateService(transit).Evaluate(Market.US, PostalCode.Parse("10001"));

        Assert.False(decision.IsServiceable);
        Assert.Equal(ServiceabilityDenialReason.EvaluationFailed, decision.Denial);
        Assert.Equal("serviceability-evaluation-failed", decision.Code);
    }

    [Fact]
    [Requirement("CC-FUL-002")]
    public void Serviceability_data_failure_is_a_denial_never_an_acceptance()
    {
        var decision = CreateService(postalCodes: new ThrowingPostalCodeServiceabilitySource())
            .Evaluate(Market.US, PostalCode.Parse("10001"));

        Assert.False(decision.IsServiceable);
        Assert.Equal(ServiceabilityDenialReason.EvaluationFailed, decision.Denial);
    }

    [Fact]
    [Requirement("CC-FUL-002")]
    public void A_serviceable_decision_cannot_carry_a_transit_beyond_the_limit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ServiceabilityDecision.Serviceable(FulfillmentTestData.UsEast, TimeSpan.FromHours(49)));
    }
}
