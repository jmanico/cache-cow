using CacheCow.Modules.BackOffice.Auditing;
using CacheCow.Modules.BackOffice.Rbac;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CacheCow.Modules.BackOffice;

/// <summary>
/// Registration entry point for the BackOffice bounded context
/// (ARCHITECTURE.md, "Server bounded contexts" 8): dashboard RBAC —
/// role–permission matrix and enforcement (CC-DSH-002; issue 080) — and the
/// append-only audit store (CC-DSH-004, CC-ORD-006, CC-SEC-020; issue 081).
/// </summary>
public static class BackOfficeModule
{
    public static IServiceCollection AddBackOfficeModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Ports with deliberately fail-closed provisional defaults (TryAdd so
        // the host replaces them once the open decisions land):
        // - the role–permission matrix CONTENT needs human authoring (issue
        //   080, Open Questions) — no matrix is shipped, so every permission
        //   check denies until the host supplies one;
        // - the step-up re-auth max age is not ratified anywhere — no policy
        //   is shipped, so every sensitive permission check denies;
        // - the WORM storage service and its residency zone are open
        //   decisions (issue 081, Open Questions; ARCHITECTURE.md, "Known
        //   unknowns") — the default sink is unconfigured replication lag;
        //   events stay durably retained for later re-replication.
        services.TryAddSingleton<IRolePermissionMatrixProvider, UnconfiguredRolePermissionMatrixProvider>();
        services.TryAddSingleton<IStepUpPolicyProvider, UnconfiguredStepUpPolicyProvider>();
        services.TryAddSingleton<IWormReplicationSink, UnconfiguredWormReplicationSink>();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IDashboardAuthorizationService, DashboardAuthorizationService>();

        // One store instance behind both faces: the full store (append +
        // query, for the Back Office itself) and the write-only sink other
        // contexts' audit ports adapt onto in host wiring (issue 081).
        services.TryAddSingleton<InMemoryAuditStore>();
        services.TryAddSingleton<IAuditStore>(provider => provider.GetRequiredService<InMemoryAuditStore>());
        services.TryAddSingleton<IAuditEventSink>(provider => provider.GetRequiredService<InMemoryAuditStore>());

        return services;
    }
}
