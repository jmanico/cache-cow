using CacheCow.Modules.BackOffice.Auditing;
using CacheCow.Modules.BackOffice.Dashboard;
using CacheCow.Modules.BackOffice.Inventory;
using CacheCow.Modules.BackOffice.Orders;
using CacheCow.Modules.BackOffice.Partners;
using CacheCow.Modules.BackOffice.Rbac;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CacheCow.Modules.BackOffice;

/// <summary>
/// Registration entry point for the BackOffice bounded context
/// (ARCHITECTURE.md, "Server bounded contexts" 8): dashboard RBAC —
/// role–permission matrix and enforcement (CC-DSH-002; issue 080) — the
/// append-only audit store (CC-DSH-004, CC-ORD-006, CC-SEC-020; issue 081),
/// and the CC-DSH-003 dashboard modules for order management (issue 082),
/// inventory by cold store (issue 084), and partner management (issue 085).
///
/// HOST WIRING CONTRACT — the host must, from outside this module:
/// <list type="bullet">
/// <item>call <c>MapBackOfficeDashboard()</c> (this module never maps itself
/// into the pipeline), on the dashboard origin only, after authentication and
/// authorization middleware — see <see cref="Api.DashboardEndpoints"/> for the
/// full boundary contract (origin isolation, staff SSO, step-up, antiforgery,
/// rate-limiter policies);</item>
/// <item>supply the role–permission matrix and the step-up max age, both of
/// which are unresolved and therefore fail closed by default (below);</item>
/// <item>supply the cross-context port adapters, which have NO defaults on
/// purpose: <see cref="IDashboardOrderReader"/>,
/// <see cref="IDashboardOrderCommands"/> (Ordering &amp; Payments — issue 035
/// owns the CC-ORD-006 state machine these delegate to);
/// <see cref="IDashboardInventoryReader"/> (Catalog &amp; Inventory — issue
/// 030); <see cref="IDashboardPartnerDirectory"/> and
/// <see cref="IDashboardPartnerWorkflow"/> (Wholesale &amp; B2B — issue 049's
/// onboarding workflow, with <see cref="DashboardActorReference"/> mapped onto
/// that context's own actor proof). A missing adapter fails loudly at
/// resolution rather than silently degrading — there is no in-module
/// stand-in that could ever answer with real orders, stock, or partners.</item>
/// </list>
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

        // The staff context is read from the host-authenticated principal's
        // claims; the host replaces this if its step-up implementation (issue
        // 060) settles on different claim names (see DashboardClaimTypes).
        services.TryAddSingleton<IStaffContextFactory, ClaimsStaffContextFactory>();

        // The CC-DSH-003 dashboard modules (issues 082/084/085). Each depends
        // on host-supplied port adapters, so resolution is deferred to first
        // use via factories rather than failing host boot while no endpoint
        // consumes them yet — the same pattern the Wholesale module uses.
        services.TryAddSingleton<IDashboardOrderService>(provider => new DashboardOrderService(
            provider.GetRequiredService<IDashboardAuthorizationService>(),
            provider.GetRequiredService<IDashboardOrderReader>(),
            provider.GetRequiredService<IDashboardOrderCommands>(),
            provider.GetRequiredService<IAuditEventSink>(),
            provider.GetRequiredService<TimeProvider>()));

        services.TryAddSingleton<IDashboardInventoryService>(provider => new DashboardInventoryService(
            provider.GetRequiredService<IDashboardAuthorizationService>(),
            provider.GetRequiredService<IDashboardInventoryReader>()));

        services.TryAddSingleton<IDashboardPartnerService>(provider => new DashboardPartnerService(
            provider.GetRequiredService<IDashboardAuthorizationService>(),
            provider.GetRequiredService<IDashboardPartnerDirectory>(),
            provider.GetRequiredService<IDashboardPartnerWorkflow>(),
            provider.GetRequiredService<IAuditEventSink>(),
            provider.GetRequiredService<TimeProvider>()));

        return services;
    }
}
