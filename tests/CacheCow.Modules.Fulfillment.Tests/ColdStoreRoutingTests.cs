using System.Reflection;
using CacheCow.Modules.Fulfillment.Routing;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.Fulfillment.Tests;

/// <summary>
/// Issue 044 (CC-FUL-001): every order routes to the regional cold store
/// serving its delivery address; unknown inputs fail closed with a typed
/// error and no assignment — never a default store.
/// </summary>
public sealed class ColdStoreRoutingTests
{
    private static readonly OrderReference Order = OrderReference.Parse("order-1001");

    private static OrderRoutingService CreateService() =>
        new(FulfillmentTestData.ServingRegions(), new RecordingAuditSink());

    [Theory]
    [Requirement("CC-FUL-001")]
    [InlineData("US", "10001", "cs-us-east")]
    [InlineData("US", "94103", "cs-us-west")]
    [InlineData("ES", "28001", "cs-es-madrid")]
    [InlineData("MX", "06000", "cs-mx-central")]
    [InlineData("DE", "10115", "cs-de-central")]
    [InlineData("JP", "100-0001", "cs-jp-kanto")]
    [InlineData("IN", "560001", "cs-in-south")]
    public void Order_routes_to_the_regional_cold_store_serving_the_delivery_address(
        string marketCode, string postalCode, string expectedStore)
    {
        var result = CreateService().RouteOrder(
            Order, Market.Parse(marketCode), PostalCode.Parse(postalCode));

        Assert.True(result.IsRouted);
        Assert.Equal(RoutingFailureReason.None, result.Failure);
        Assert.NotNull(result.Assignment);
        Assert.Equal(ColdStoreId.Parse(expectedStore), result.Assignment.AssignedStore);
        Assert.Equal(result.Assignment.ServingStore, result.Assignment.AssignedStore);
        Assert.False(result.Assignment.IsCrossRegion);
        Assert.Equal(Order, result.Assignment.Order);
    }

    [Theory]
    [Requirement("CC-FUL-001")]
    [InlineData("US", "99999")]
    [InlineData("DE", "80331")]
    [InlineData("IN", "110001")]
    public void Unserved_postal_code_fails_closed_with_no_assignment(string marketCode, string postalCode)
    {
        var result = CreateService().RouteOrder(
            Order, Market.Parse(marketCode), PostalCode.Parse(postalCode));

        Assert.False(result.IsRouted);
        Assert.Null(result.Assignment);
        Assert.Equal(RoutingFailureReason.PostalCodeNotServed, result.Failure);
    }

    [Fact]
    [Requirement("CC-FUL-001")]
    public void Market_without_serving_region_data_fails_closed()
    {
        var emptySource = new InMemoryServingRegionSource([]);
        var service = new OrderRoutingService(emptySource, new RecordingAuditSink());

        var result = service.RouteOrder(Order, Market.JP, PostalCode.Parse("100-0001"));

        Assert.False(result.IsRouted);
        Assert.Null(result.Assignment);
        Assert.Equal(RoutingFailureReason.MarketNotServed, result.Failure);
    }

    [Fact]
    [Requirement("CC-FUL-001")]
    public void Serving_region_lookup_failure_is_a_denial_never_a_fallback_assignment()
    {
        var service = new OrderRoutingService(new ThrowingServingRegionSource(), new RecordingAuditSink());

        var result = service.RouteOrder(Order, Market.US, PostalCode.Parse("10001"));

        Assert.False(result.IsRouted);
        Assert.Null(result.Assignment);
        Assert.Equal(RoutingFailureReason.ResolutionFailed, result.Failure);
    }

    [Fact]
    [Requirement("CC-FUL-001")]
    public void Ambiguous_serving_region_data_is_rejected_at_construction()
    {
        Assert.Throws<ArgumentException>(() => new InMemoryServingRegionSource(
        [
            new ServingRegionEntry(Market.US, PostalCode.Parse("10001"), FulfillmentTestData.UsEast),
            new ServingRegionEntry(Market.US, PostalCode.Parse("10001"), FulfillmentTestData.UsWest),
        ]));
    }

    [Fact]
    [Requirement("CC-FUL-001")]
    public void No_routing_api_reaches_a_cross_region_store_without_an_override_permission()
    {
        // AC-02, by construction: RouteOrder takes no target store, and every
        // public routing method that accepts a target ColdStoreId also
        // requires the typed OverrideAuthorization proof.
        foreach (var method in typeof(OrderRoutingService).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var parameters = method.GetParameters();
            if (parameters.Any(p => p.ParameterType == typeof(ColdStoreId)))
            {
                Assert.Contains(parameters, p => p.ParameterType == typeof(OverrideAuthorization));
            }
        }
    }
}
