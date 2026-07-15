using CacheCow.Modules.Fulfillment.Routing;
using CacheCow.Modules.Fulfillment.Serviceability;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CacheCow.Modules.Fulfillment.Tests;

/// <summary>
/// Issues 044/045: the module registers resolvable services whose provisional
/// port defaults fail closed — no serving-region or serviceability data, no
/// transit estimates, and an audit sink that denies every append — until the
/// host wires the real adapters (open operational data and issue 081).
/// </summary>
public sealed class ModuleRegistrationTests
{
    private static ServiceProvider BuildProvider() =>
        new ServiceCollection().AddFulfillmentModule().BuildServiceProvider();

    [Fact]
    [Requirement("CC-FUL-001")]
    [Requirement("CC-FUL-002")]
    public void Module_registers_routing_and_serviceability_services()
    {
        using var provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService<OrderRoutingService>());
        Assert.NotNull(provider.GetRequiredService<CheckoutServiceabilityService>());
    }

    [Fact]
    [Requirement("CC-FUL-001")]
    public void Default_wiring_routes_nothing_fail_closed()
    {
        using var provider = BuildProvider();
        var routing = provider.GetRequiredService<OrderRoutingService>();

        var result = routing.RouteOrder(
            OrderReference.Parse("order-1"), Market.US, PostalCode.Parse("10001"));

        Assert.False(result.IsRouted);
        Assert.Equal(RoutingFailureReason.MarketNotServed, result.Failure);
    }

    [Fact]
    [Requirement("CC-FUL-001")]
    public void Default_wiring_denies_every_cross_region_override_fail_closed()
    {
        using var provider = BuildProvider();
        var routing = provider.GetRequiredService<OrderRoutingService>();
        var assignment = new ColdStoreAssignment(
            OrderReference.Parse("order-1"), FulfillmentTestData.UsEast, FulfillmentTestData.UsEast);

        var result = routing.ApplyCrossRegionOverride(
            assignment, FulfillmentTestData.UsWest, OverrideAuthorization.IssuedTo("staff-ops-7"));

        Assert.False(result.IsApplied);
        Assert.NotEqual(OverrideDenialReason.None, result.Denial);
    }

    [Fact]
    [Requirement("CC-FUL-002")]
    public void Default_wiring_accepts_no_order_as_serviceable_fail_closed()
    {
        using var provider = BuildProvider();
        var serviceability = provider.GetRequiredService<CheckoutServiceabilityService>();

        var decision = serviceability.Evaluate(Market.DE, PostalCode.Parse("10115"));

        Assert.False(decision.IsServiceable);
        Assert.Equal(ServiceabilityDenialReason.PostalCodeNotServiceable, decision.Denial);
    }
}
