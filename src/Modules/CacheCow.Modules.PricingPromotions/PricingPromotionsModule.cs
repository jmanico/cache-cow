using CacheCow.Modules.PricingPromotions.Promotions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CacheCow.Modules.PricingPromotions;

/// <summary>
/// Registration entry point for the Pricing &amp; Promotions bounded context
/// (ARCHITECTURE.md, "Server bounded contexts" 3): per-SKU per-market prices in
/// integer minor units (CC-PRC-001/003, issue 032), the promotion evaluation
/// engine (CC-PRC-006/007, issue 033), and locale-aware price formatting with
/// market tax-display conventions (CC-PRC-002/004, issue 034).
/// </summary>
public static class PricingPromotionsModule
{
    public static IServiceCollection AddPricingPromotionsModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);

        // Provisional fail-closed default (TryAdd so the host replaces it):
        // the market-to-IANA-timezone mapping is an open decision (issue 033,
        // Open Questions), so no default mapping exists — until the host
        // supplies a configured MarketTimeZoneMap, promotion evaluation fails
        // closed rather than guessing a zone. Likewise, every
        // rounding-sensitive operation takes an explicit RoundingMode; the
        // rounding policy is unratified (issues 002/033/034).
        services.TryAddSingleton<IMarketTimeZoneProvider, UnconfiguredMarketTimeZoneProvider>();

        services.TryAddSingleton<IPromotionEvaluator, PromotionEvaluator>();

        return services;
    }
}
