using CacheCow.Modules.CatalogInventory.Catalog;
using CacheCow.Modules.CatalogInventory.Inventory;
using CacheCow.Modules.CatalogInventory.Search;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CacheCow.Modules.CatalogInventory.Tests;

/// <summary>
/// Issues 029–031: the bounded context registers its services and ports with
/// provisional in-memory defaults the host can replace (TryAdd), and the
/// default composition fails closed.
/// </summary>
public sealed class ModuleRegistrationTests
{
    [Fact]
    [Requirement("CC-CAT-001")]
    [Requirement("CC-CAT-002")]
    [Requirement("CC-CAT-005")]
    public void The_module_registers_domain_services_and_ports()
    {
        var services = new ServiceCollection().AddCatalogInventoryModule();
        using var provider = services.BuildServiceProvider();

        Assert.IsType<InMemorySkuCatalog>(provider.GetRequiredService<ISkuCatalog>());
        Assert.IsType<InMemoryInventoryLedger>(provider.GetRequiredService<IInventoryLedger>());
        Assert.IsType<InMemoryMarketColdStoreMap>(provider.GetRequiredService<IMarketColdStoreMap>());
        Assert.IsType<InMemoryCatalogSearchService>(provider.GetRequiredService<ICatalogSearchService>());
        Assert.NotNull(provider.GetRequiredService<AvailabilityService>());
    }

    [Fact]
    [Requirement("CC-CAT-002")]
    public void The_host_can_replace_a_port_with_its_own_composition()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMarketColdStoreMap>(new HostSuppliedMap());
        services.AddCatalogInventoryModule();
        using var provider = services.BuildServiceProvider();

        Assert.IsType<HostSuppliedMap>(provider.GetRequiredService<IMarketColdStoreMap>());
    }

    [Fact]
    [Requirement("CC-CAT-003")]
    public void The_default_composition_fails_closed_with_no_topology_configured()
    {
        var services = new ServiceCollection().AddCatalogInventoryModule();
        using var provider = services.BuildServiceProvider();
        var availability = provider.GetRequiredService<AvailabilityService>();

        // No cold-store topology configured: everything is unavailable-in-
        // region, never in-stock (issue 030, Failure Behavior).
        Assert.Equal(
            SkuAvailability.UnavailableInRegion,
            availability.DeriveFromStock(SkuId.Parse("ANY-SKU"), Market.US));
    }

    private sealed class HostSuppliedMap : IMarketColdStoreMap
    {
        public IReadOnlyCollection<ColdStoreId> StoresServing(Market market) => [];
    }
}
