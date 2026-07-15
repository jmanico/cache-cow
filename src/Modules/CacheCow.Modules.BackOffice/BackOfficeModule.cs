using Microsoft.Extensions.DependencyInjection;

namespace CacheCow.Modules.BackOffice;

/// <summary>
/// Registration entry point for the BackOffice bounded context
/// (ARCHITECTURE.md, "Server bounded contexts"). Business logic lands in its
/// own epic sub-issue; the scaffold carries structure only (issue 001, AC-06).
/// </summary>
public static class BackOfficeModule
{
    public static IServiceCollection AddBackOfficeModule(this IServiceCollection services)
    {
        return services;
    }
}
