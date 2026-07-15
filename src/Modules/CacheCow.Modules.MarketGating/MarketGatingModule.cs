using Microsoft.Extensions.DependencyInjection;

namespace CacheCow.Modules.MarketGating;

/// <summary>
/// Registration entry point for the MarketGating bounded context
/// (ARCHITECTURE.md, "Server bounded contexts"). Business logic lands in its
/// own epic sub-issue; the scaffold carries structure only (issue 001, AC-06).
/// </summary>
public static class MarketGatingModule
{
    public static IServiceCollection AddMarketGatingModule(this IServiceCollection services)
    {
        return services;
    }
}
