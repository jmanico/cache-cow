using CacheCow.Modules.Fulfillment.Auditing;
using CacheCow.Modules.Fulfillment.Routing;
using CacheCow.Modules.Fulfillment.Serviceability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CacheCow.Modules.Fulfillment;

/// <summary>
/// Registration entry point for the Fulfillment bounded context
/// (ARCHITECTURE.md, "Server bounded contexts" item 5): routing to the
/// regional cold store for the delivery address with audited cross-region
/// override (CC-FUL-001), and frozen-shipping serviceability checks at
/// checkout (CC-FUL-002).
/// </summary>
public static class FulfillmentModule
{
    public static IServiceCollection AddFulfillmentModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Ports with deliberately fail-closed provisional defaults (TryAdd so
        // the host replaces them once the open decisions land):
        // - serving-region and serviceable-postal-code data are operational
        //   data not defined in the specs (issues 044/045, Open Questions;
        //   persistence additionally AT RISK on the residency/write-region
        //   conflict, ARCHITECTURE.md "Known unknowns") — empty in-memory
        //   sources deny everything until real data is supplied;
        // - the EasyPost transit adapter is a later issue — no estimate is
        //   available, so no order passes the 48-hour check optimistically;
        // - the append-only audit store is issue 081 — appends fail, so
        //   cross-region overrides are denied until the host wires it.
        services.TryAddSingleton<IServingRegionSource>(_ => new InMemoryServingRegionSource([]));
        services.TryAddSingleton<IPostalCodeServiceabilitySource>(_ => new InMemoryPostalCodeServiceabilitySource([]));
        services.TryAddSingleton<ITransitTimeEstimator, UnavailableTransitTimeEstimator>();
        services.TryAddSingleton<IFulfillmentAuditSink, UnconfiguredFulfillmentAuditSink>();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<OrderRoutingService>();
        services.TryAddSingleton<CheckoutServiceabilityService>();

        return services;
    }
}
