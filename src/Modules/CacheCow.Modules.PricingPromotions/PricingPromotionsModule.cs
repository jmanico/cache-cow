using Microsoft.Extensions.DependencyInjection;

namespace CacheCow.Modules.PricingPromotions;

/// <summary>
/// Registration entry point for the PricingPromotions bounded context
/// (ARCHITECTURE.md, "Server bounded contexts"). Business logic lands in its
/// own epic sub-issue; the scaffold carries structure only (issue 001, AC-06).
/// </summary>
public static class PricingPromotionsModule
{
    public static IServiceCollection AddPricingPromotionsModule(this IServiceCollection services)
    {
        return services;
    }
}
