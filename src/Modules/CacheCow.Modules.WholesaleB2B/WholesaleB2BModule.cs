using Microsoft.Extensions.DependencyInjection;

namespace CacheCow.Modules.WholesaleB2B;

/// <summary>
/// Registration entry point for the WholesaleB2B bounded context
/// (ARCHITECTURE.md, "Server bounded contexts"). Business logic lands in its
/// own epic sub-issue; the scaffold carries structure only (issue 001, AC-06).
/// </summary>
public static class WholesaleB2BModule
{
    public static IServiceCollection AddWholesaleB2BModule(this IServiceCollection services)
    {
        return services;
    }
}
