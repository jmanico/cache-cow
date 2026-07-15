using CacheCow.Modules.WholesaleB2B.Auth;
using CacheCow.Modules.WholesaleB2B.Gating;
using CacheCow.Modules.WholesaleB2B.Idempotency;
using CacheCow.Modules.WholesaleB2B.Invoices;
using CacheCow.Modules.WholesaleB2B.Orders;
using CacheCow.Modules.WholesaleB2B.Partners;
using CacheCow.Modules.WholesaleB2B.PriceLists;
using CacheCow.Modules.WholesaleB2B.RateLimits;
using CacheCow.Modules.WholesaleB2B.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CacheCow.Modules.WholesaleB2B;

/// <summary>
/// Registration entry point for the Wholesale &amp; B2B API bounded context
/// (ARCHITECTURE.md, "Server bounded contexts" 6): partner tenancy and the
/// dashboard-driven onboarding approval workflow (issue 049, CC-WHS-002),
/// tenant-scoped wholesale price lists with net-60-default payment terms
/// (issue 050, CC-WHS-001/003/004), and the versioned /v1 B2B API with its
/// token policy, scope/tenant enforcement, rate-limit contract, and outbound
/// partner webhooks (issues 053–057, CC-API-001–010).
///
/// HOST WIRING CONTRACT — the host must, from outside this module:
/// - call <c>MapWholesaleB2BApi()</c> (this module never maps itself into the
///   pipeline) after authentication/authorization middleware (SECURITY.md,
///   HTTP boundary rule 5);
/// - configure JwtBearer for the B2B audience per SECURITY.md Authentication
///   rule 7: ValidateIssuer/ValidateAudience/ValidateLifetime/
///   ValidateIssuerSigningKey all true, pinned ValidAlgorithms (reject
///   alg=none), clock skew ≤ 2 minutes, audience = this API's resource
///   identifier, Entra ID as authorization server (client credentials with
///   private_key_jwt or mTLS per RFC 9700/8705 — CC-API-002). Signature-level
///   sender-constraint proof (mTLS binding / DPoP proof) is host transport
///   scope; this module enforces the claim-level policy via
///   <see cref="IB2BTokenClaimsValidator"/>;
/// - register rate-limiter policies named
///   <see cref="B2BRateLimitPolicies.Client"/> (600/min default) and
///   <see cref="B2BRateLimitPolicies.OrderCreation"/> (60/min, the host's
///   existing order-creation policy), partitioned per authenticated client via
///   <see cref="B2BRateLimitPartition.KeyFor"/> with budgets from
///   <see cref="IB2BRateLimitTierSource"/>; 429 + Retry-After semantics are
///   host middleware (issue 019, CC-API-008);
/// - supply the port adapters: <see cref="IPartnerAuditSink"/> (append-only
///   audit, issue 081), <see cref="IB2BClientDirectory"/> (Entra client-id →
///   partner tenancy), <see cref="IB2BGatingCheck"/> (Market &amp; Gating
///   Policy service — CC-API-007, ARCHITECTURE.md Dependency rule 1),
///   <see cref="IWholesaleInvoiceReader"/> (Invoicing context),
///   <see cref="IWebhookAddressResolver"/> (real DNS resolution),
///   <see cref="IWebhookSecretSource"/> (Key Vault per-partner rotating HMAC
///   secrets), and <see cref="IWebhookDeliveryTransport"/> (no-redirect HTTPS
///   client per the port contract);
/// - keep API hosts off plaintext HTTP entirely (SECURITY.md, HTTP boundary
///   rule 1) — transport is host scope;
/// - the dashboard HTTP boundary that authenticates staff and mints
///   <see cref="DashboardActorProof"/> (issues 020/080/085), and the portal
///   session layer for human buyers (issue 051 — blocked on the portal-IdP
///   open decision, ARCHITECTURE.md "Known unknowns").
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

        // Issue 054: claim-level token policy. Factory deliberately — the
        // client directory is a host-supplied adapter, resolved on first use.
        services.TryAddSingleton<IB2BTokenClaimsValidator>(provider => new B2BTokenClaimsValidator(
            provider.GetRequiredService<IB2BClientDirectory>(),
            provider.GetService<TimeProvider>()));

        // Issues 053/055: in-memory tenant-scoped order store until the
        // durable wholesale schema lands (issue 015).
        services.TryAddSingleton<InMemoryWholesaleOrders>();
        services.TryAddSingleton<IWholesaleOrders>(provider =>
            provider.GetRequiredService<InMemoryWholesaleOrders>());

        // CC-API-005/CC-SEC-015: (client, key)-scoped, fingerprint-bound
        // idempotency; the host replaces this with the durable adapter over
        // the Ordering & Payments store.
        services.TryAddSingleton<InMemoryB2BOrderIdempotency>();
        services.TryAddSingleton<IB2BOrderIdempotency>(provider =>
            provider.GetRequiredService<InMemoryB2BOrderIdempotency>());

        // CC-API-008: tier budgets as configuration data (ratified defaults
        // 600/min, order creation 60/min; overrides per partner).
        services.TryAddSingleton<InMemoryB2BRateLimitTierSource>();
        services.TryAddSingleton<IB2BRateLimitTierSource>(provider =>
            provider.GetRequiredService<InMemoryB2BRateLimitTierSource>());

        // Issue 057: webhook registry (SSRF-validated at registration; the
        // resolver is a host-supplied adapter) and the signing/delivery
        // pipeline over host-supplied Key Vault and transport ports.
        services.TryAddSingleton<IPartnerWebhookRegistry>(provider => new InMemoryPartnerWebhookRegistry(
            provider.GetRequiredService<IWebhookAddressResolver>(),
            provider.GetService<TimeProvider>()));
        services.TryAddSingleton(provider => new WebhookDeliveryService(
            provider.GetRequiredService<IPartnerWebhookRegistry>(),
            provider.GetRequiredService<IWebhookSecretSource>(),
            provider.GetRequiredService<IWebhookAddressResolver>(),
            provider.GetRequiredService<IWebhookDeliveryTransport>(),
            provider.GetService<TimeProvider>()));

        return services;
    }
}
