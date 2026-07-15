using Microsoft.Extensions.DependencyInjection;

namespace CacheCow.Modules.Invoicing;

/// <summary>
/// Registration entry point for the Invoicing bounded context
/// (ARCHITECTURE.md, "Server bounded contexts"). Business logic lands in its
/// own epic sub-issue; the scaffold carries structure only (issue 001, AC-06).
/// </summary>
public static class InvoicingModule
{
    public static IServiceCollection AddInvoicingModule(this IServiceCollection services)
    {
        return services;
    }
}
