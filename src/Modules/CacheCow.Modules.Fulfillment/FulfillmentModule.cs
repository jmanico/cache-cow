using Microsoft.Extensions.DependencyInjection;

namespace CacheCow.Modules.Fulfillment;

/// <summary>
/// Registration entry point for the Fulfillment bounded context
/// (ARCHITECTURE.md, "Server bounded contexts"). Business logic lands in its
/// own epic sub-issue; the scaffold carries structure only (issue 001, AC-06).
/// </summary>
public static class FulfillmentModule
{
    public static IServiceCollection AddFulfillmentModule(this IServiceCollection services)
    {
        return services;
    }
}
