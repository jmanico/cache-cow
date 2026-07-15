using Microsoft.Extensions.DependencyInjection;

namespace CacheCow.Modules.OrderingPayments;

/// <summary>
/// Registration entry point for the OrderingPayments bounded context
/// (ARCHITECTURE.md, "Server bounded contexts"). Business logic lands in its
/// own epic sub-issue; the scaffold carries structure only (issue 001, AC-06).
/// </summary>
public static class OrderingPaymentsModule
{
    public static IServiceCollection AddOrderingPaymentsModule(this IServiceCollection services)
    {
        return services;
    }
}
