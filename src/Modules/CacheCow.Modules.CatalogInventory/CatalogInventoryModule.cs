using CacheCow.Modules.CatalogInventory.Catalog;
using CacheCow.Modules.CatalogInventory.Inventory;
using CacheCow.Modules.CatalogInventory.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CacheCow.Modules.CatalogInventory;

/// <summary>
/// Registration entry point for the Catalog &amp; Inventory bounded context
/// (ARCHITECTURE.md, "Server bounded contexts" 2): structured SKU food data
/// (issue 029), per-SKU per-cold-store inventory with three-state availability
/// (issue 030), and per-market per-locale search (issue 031).
/// </summary>
public static class CatalogInventoryModule
{
    public static IServiceCollection AddCatalogInventoryModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Ports with deliberately provisional in-memory defaults (TryAdd so the
        // host replaces them when the PostgreSQL persistence adapter lands,
        // issue 015). The empty cold-store map default fails closed: every
        // availability derivation is unavailable-in-region until the host
        // supplies the real fulfillment topology (issue 030, Failure Behavior).
        services.TryAddSingleton<ISkuCatalog, InMemorySkuCatalog>();
        services.TryAddSingleton<IInventoryLedger, InMemoryInventoryLedger>();
        services.TryAddSingleton<IMarketColdStoreMap, InMemoryMarketColdStoreMap>();
        services.TryAddSingleton<ICatalogSearchService, InMemoryCatalogSearchService>();

        services.TryAddSingleton<AvailabilityService>();

        return services;
    }
}
