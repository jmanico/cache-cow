using CacheCow.Modules.OrderingPayments.Idempotency;
using CacheCow.Modules.OrderingPayments.Orders;
using CacheCow.Modules.OrderingPayments.Submission;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CacheCow.Modules.OrderingPayments;

/// <summary>
/// Registration entry point for the Ordering &amp; Payments bounded context
/// (ARCHITECTURE.md, "Server bounded contexts" 4): order state machine
/// (issue 035), server-side order submission (issue 036), and the scoped,
/// fingerprint-bound idempotency service (issue 037).
///
/// The host must additionally supply, from outside this module:
/// - <see cref="ICanonicalPriceSource"/>, <see cref="IPromotionEvaluator"/>
///   (adapters over the Pricing &amp; Promotions context — this module never
///   references it, ARCHITECTURE.md, Dependency rule 2),
/// - <see cref="ITaxCalculator"/> (Stripe Tax / Razorpay adapters),
/// - <see cref="IAuditSink"/> (append-only audit store, issue 081),
/// - <see cref="OrderSubmissionOptions"/> and <see cref="IdempotencyOptions"/>
///   (required configuration with no defaults),
/// - a ratified <see cref="BranchTransitionTable"/> once the branch-legality
///   open decision lands (issue 035, Open Questions) — until then the state
///   machine fails every cancelled/refunded transition closed.
/// </summary>
public static class OrderingPaymentsModule
{
    public static IServiceCollection AddOrderingPaymentsModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // First-party default fingerprint strategy; the canonicalization
        // question stays open (issue 037, Open Questions) — TryAdd so the host
        // can replace it when the decision lands.
        services.TryAddSingleton<IRequestFingerprintStrategy, Sha256RequestFingerprintStrategy>();

        // The remaining registrations use factories deliberately: their
        // dependencies are host-supplied adapters and required options (see
        // the type's docs) that land with later issues, so resolution is
        // deferred to first use instead of failing host boot validation while
        // no endpoint consumes them yet.

        // In-memory store until the durable PostgreSQL store lands (its own
        // persistence issue); requires IdempotencyOptions from the host.
        services.TryAddSingleton<IIdempotencyStore>(provider => new InMemoryIdempotencyStore(
            provider.GetRequiredService<IdempotencyOptions>(),
            provider.GetService<TimeProvider>()));

        services.TryAddSingleton(provider => new IdempotencyService(
            provider.GetRequiredService<IIdempotencyStore>(),
            provider.GetRequiredService<IRequestFingerprintStrategy>()));

        services.TryAddSingleton(provider => new OrderSubmissionService(
            provider.GetRequiredService<ICanonicalPriceSource>(),
            provider.GetRequiredService<IPromotionEvaluator>(),
            provider.GetRequiredService<ITaxCalculator>(),
            provider.GetRequiredService<OrderSubmissionOptions>(),
            provider.GetService<TimeProvider>()));

        services.TryAddSingleton(provider => new OrderStateMachine(
            provider.GetRequiredService<IAuditSink>(),
            provider.GetService<BranchTransitionTable>(),
            provider.GetService<TimeProvider>()));

        return services;
    }
}
