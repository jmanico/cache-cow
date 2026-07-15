using CacheCow.Modules.MarketGating.Enforcement;
using CacheCow.Modules.MarketGating.Resolution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CacheCow.Modules.MarketGating;

/// <summary>
/// Registration entry point for the Market &amp; Gating Policy bounded context
/// (ARCHITECTURE.md, "Server bounded contexts" 1) — the single server-side
/// enforcement point consulted by storefront rendering, search, the B2B API,
/// and sitemap/feed generation (Dependency rule 1).
/// </summary>
public static class MarketGatingModule
{
    public static IServiceCollection AddMarketGatingModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Ports with deliberately provisional defaults (TryAdd so the host can
        // replace them once the open decisions land):
        // - preference persistence is blocked on the residency/write-region
        //   decision (ARCHITECTURE.md, "Known unknowns"; issue 024 AT RISK);
        // - no geolocation provider is named in the canonical docs
        //   (issue 024, Open Questions).
        services.TryAddSingleton<IMarketPreferenceStore, InMemoryMarketPreferenceStore>();
        services.TryAddSingleton<IGeolocationMarketProposer, NullGeolocationMarketProposer>();

        services.TryAddSingleton<TransactingContextResolver>();
        services.TryAddSingleton<IMarketGatingService, MarketGatingService>();

        return services;
    }
}
