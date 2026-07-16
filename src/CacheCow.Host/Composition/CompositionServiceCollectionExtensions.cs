using CacheCow.Modules.BackOffice.Auditing;
using CacheCow.Modules.CatalogInventory.Catalog;
using CacheCow.Modules.Fulfillment.Auditing;
using CacheCow.Modules.Invoicing.Access;
using CacheCow.Modules.MarketGating.Enforcement;
using CacheCow.Modules.OrderingPayments.GuestAccess;
using CacheCow.Modules.OrderingPayments.Orders;
using CacheCow.Modules.OrderingPayments.Payments;
using CacheCow.Modules.OrderingPayments.Submission;
using CacheCow.Modules.OrderingPayments.Webhooks;
using CacheCow.Modules.PricingPromotions.Pricing;
using CacheCow.Modules.WholesaleB2B.Auth;
using CacheCow.Modules.WholesaleB2B.Gating;
using CacheCow.Modules.WholesaleB2B.Invoices;
using CacheCow.Modules.WholesaleB2B.Partners;
using CacheCow.Modules.WholesaleB2B.Webhooks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PricingPromotionEvaluator = CacheCow.Modules.PricingPromotions.Promotions.IPromotionEvaluator;

namespace CacheCow.Host.Composition;

/// <summary>
/// The host composition root for cross-module ports (ARCHITECTURE.md:
/// modules reference only the shared kernel — every cross-context need is a
/// port the HOST adapts; Dependency rules 1, 2, 9). Called AFTER the module
/// registrations so these registrations replace the modules' provisional
/// TryAdd defaults, and after <c>AddCacheCowSecurity</c>.
///
/// Wiring here is limited to composition:
/// - audit emission ports → the BackOffice append-only store (CC-DSH-004),
/// - B2B gating → the real Market &amp; Gating service (CC-API-007),
/// - Ordering money ports → the Pricing &amp; Promotions canonical price list
///   and promotion engine (CC-PRC-005/006),
/// - Invoicing guest-token validation → the Ordering capability-token
///   service (CC-ORD-010, CC-INV-002),
/// - fail-closed defaults for ports whose real adapters are later issues.
///
/// Unratified configuration (rounding mode, guest-token lifetime, idempotency
/// and webhook windows, submission bounds, market timezones) is deliberately
/// NOT given values here: dependent paths fail closed at first use until a
/// human ratifies each (CLAUDE.md working rules; ARCHITECTURE.md, "Known
/// unknowns" discipline). Registrations that depend on possibly-absent
/// services use factories so host boot validation (ValidateOnBuild) stays
/// green while those paths stay fail-closed.
/// </summary>
public static class CompositionServiceCollectionExtensions
{
    public static IServiceCollection AddCacheCowComposition(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // ---- Audit adapters (CC-DSH-004; SECURITY.md, Logging rule 6) ------
        // The BackOffice module registers IAuditEventSink over its append-only
        // store; each emitting context's port adapts onto it.
        services.AddSingleton<IAuditSink, OrderingPaymentsAuditSinkAdapter>();
        services.AddSingleton<IPartnerAuditSink, PartnerAuditSinkAdapter>();

        // The Fulfillment module TryAdds a fail-closed default; Replace swaps
        // in the real adapter (registration order: after the modules).
        services.Replace(ServiceDescriptor.Singleton<IFulfillmentAuditSink, FulfillmentAuditSinkAdapter>());

        // ---- Gating adapter (CC-API-007; ARCHITECTURE.md, Dependency rule 1)
        services.AddSingleton<IB2BGatingCheck, B2BMarketGatingCheckAdapter>();

        // ---- Money-path adapters (CC-PRC-001/005/006) ----------------------
        // The canonical PriceList is operational pricing data whose durable
        // store is a later persistence issue; no price data is invented here.
        // Factories defer resolution to first use: while no PriceList is
        // registered, consumer order submission fails closed instead of the
        // host failing boot validation.
        services.AddSingleton<MoneyRoundingPolicy>(provider =>
            MoneyRoundingPolicy.FromConfiguration(provider.GetRequiredService<IConfiguration>()));
        services.TryAddSingleton<IActivePromotionSource, NoPromotionsConfiguredSource>();

        services.AddSingleton<ICanonicalPriceSource>(provider => new PriceListCanonicalPriceSource(
            provider.GetRequiredService<PriceList>(),
            provider.GetRequiredService<ISkuCatalog>(),
            provider.GetRequiredService<IMarketGatingService>()));

        services.AddSingleton<IPromotionEvaluator>(provider => new PricingPromotionEvaluatorAdapter(
            provider.GetRequiredService<PricingPromotionEvaluator>(),
            provider.GetRequiredService<PriceList>(),
            provider.GetRequiredService<ISkuCatalog>(),
            provider.GetRequiredService<IActivePromotionSource>(),
            provider.GetRequiredService<MoneyRoundingPolicy>()));

        // ---- Guest capability-token adapter (CC-ORD-010, CC-INV-002) ------
        // Factory: GuestAccessTokenService requires GuestAccessOptions, whose
        // token lifetime is an unratified open decision — the guest invoice
        // path fails closed at first use until a human supplies it.
        services.AddSingleton<IGuestOrderCapabilityTokenValidator>(provider =>
            new GuestOrderCapabilityTokenValidatorAdapter(
                provider.GetRequiredService<GuestAccessTokenService>()));

        // ---- Fail-closed defaults for later-issue adapters -----------------
        // TryAdd so tests and the eventual real adapters replace them; every
        // default throws (denial), never a permissive stub.
        services.TryAddSingleton<IB2BClientDirectory, UnconfiguredB2BClientDirectory>();
        services.TryAddSingleton<ITaxCalculator, UnconfiguredTaxCalculator>();
        services.TryAddSingleton<IProcessorStatusClient, UnconfiguredProcessorStatusClient>();
        services.TryAddSingleton<ISigningSecretProvider, UnconfiguredSigningSecretProvider>();
        services.TryAddSingleton<IWebhookAddressResolver, UnconfiguredWebhookAddressResolver>();
        services.TryAddSingleton<IWebhookSecretSource, UnconfiguredWebhookSecretSource>();
        services.TryAddSingleton<IWebhookDeliveryTransport, UnconfiguredWebhookDeliveryTransport>();
        services.TryAddSingleton<IWholesaleInvoiceReader, UnconfiguredWholesaleInvoiceReader>();

        return services;
    }
}
