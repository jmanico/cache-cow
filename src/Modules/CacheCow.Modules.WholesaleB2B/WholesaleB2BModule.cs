using CacheCow.Modules.WholesaleB2B.Partners;
using CacheCow.Modules.WholesaleB2B.PriceLists;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CacheCow.Modules.WholesaleB2B;

/// <summary>
/// Registration entry point for the Wholesale &amp; B2B API bounded context
/// (ARCHITECTURE.md, "Server bounded contexts" 6): partner tenancy and the
/// dashboard-driven onboarding approval workflow (issue 049, CC-WHS-002), and
/// tenant-scoped wholesale price lists with net-60-default payment terms
/// (issue 050, CC-WHS-001/003/004).
///
/// The host must additionally supply, from outside this module:
/// - <see cref="IPartnerAuditSink"/> (append-only audit store, issue 081) —
///   without it the onboarding workflow cannot run, which is the fail-closed
///   default: no unaudited approval path exists (SECURITY.md, Logging rule 6),
/// - the dashboard HTTP boundary that authenticates staff and mints
///   <see cref="DashboardActorProof"/> (issues 020/080/085; SECURITY.md,
///   Authentication rules 1–2, 8),
/// - the portal/B2B session layer that mints <see cref="PartnerTenantContext"/>
///   from persisted tenancy state (issues 051/054/055 — buyer authentication is
///   blocked on the portal-IdP open decision, ARCHITECTURE.md "Known unknowns").
/// </summary>
public static class WholesaleB2BModule
{
    public static IServiceCollection AddWholesaleB2BModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Factory deliberately: IPartnerAuditSink is a host-supplied adapter
        // (issue 081), so resolution is deferred to first use instead of
        // failing host boot validation while no endpoint consumes it yet.
        services.TryAddSingleton(provider => new PartnerOnboardingWorkflow(
            provider.GetRequiredService<IPartnerAuditSink>(),
            provider.GetService<TimeProvider>()));

        // In-memory store until the durable PostgreSQL wholesale schema lands
        // (issue 015; SECURITY.md, Secret handling rule 10). Registered once,
        // exposed to consumers only through the tenant-scoped read port.
        services.TryAddSingleton<InMemoryWholesalePriceLists>();
        services.TryAddSingleton<IWholesalePriceLists>(provider =>
            provider.GetRequiredService<InMemoryWholesalePriceLists>());

        return services;
    }
}
