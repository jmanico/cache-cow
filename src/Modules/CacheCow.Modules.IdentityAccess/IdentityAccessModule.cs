using Microsoft.Extensions.DependencyInjection;

namespace CacheCow.Modules.IdentityAccess;

/// <summary>
/// Registration entry point for the IdentityAccess bounded context
/// (ARCHITECTURE.md, "Server bounded contexts"). Business logic lands in its
/// own epic sub-issue; the scaffold carries structure only (issue 001, AC-06).
/// </summary>
public static class IdentityAccessModule
{
    public static IServiceCollection AddIdentityAccessModule(this IServiceCollection services)
    {
        return services;
    }
}
