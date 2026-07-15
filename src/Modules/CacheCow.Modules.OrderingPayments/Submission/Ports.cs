using CacheCow.SharedKernel;

namespace CacheCow.Modules.OrderingPayments.Submission;

/// <summary>
/// Port toward the Pricing &amp; Promotions bounded context, the canonical
/// per-SKU per-market price source (ARCHITECTURE.md, Dependency rule 2;
/// CC-PRC-001/005). The Ordering context never references the Pricing module
/// directly — the host adapts this port to it.
/// </summary>
public interface ICanonicalPriceSource
{
    /// <summary>
    /// Canonical unit price for the SKU in the transacting market's currency,
    /// or false when the SKU is not priceable/available in that market —
    /// which rejects the submission (server-side gating consultation is
    /// ARCHITECTURE.md, Dependency rule 1; the gating service itself is the
    /// Market &amp; Gating context, adapted upstream by the host).
    /// </summary>
    bool TryGetUnitPrice(SkuId sku, Market market, out Money unitPrice);
}

/// <summary>
/// Port toward the Pricing &amp; Promotions promotion engine. The order
/// service is the final authority on applied promotions: evaluation happens at
/// submission time against the server clock, so an expired promotion a cached
/// UI still displayed never applies (CC-PRC-006).
/// </summary>
public interface IPromotionEvaluator
{
    /// <summary>
    /// The discount to apply to one line, evaluated at <paramref name="submittedAt"/>
    /// (server clock). Must be between zero and <paramref name="lineSubtotal"/>
    /// in the same currency; anything else fails the submission closed.
    /// Returns zero-money when no promotion applies.
    /// </summary>
    Money EvaluateDiscount(SkuId sku, Market market, int quantity, Money lineSubtotal, DateTimeOffset submittedAt);
}

/// <summary>
/// Port toward the external tax computation (Stripe Tax for US/ES/MX/DE/JP,
/// Razorpay/local rules for IN — ARCHITECTURE.md, "Technology decisions";
/// CC-PRC-002 display conventions are issue 034). The Ordering context never
/// computes tax itself; it delegates and records the result.
/// </summary>
public interface ITaxCalculator
{
    /// <summary>
    /// Tax on the order's post-discount total for the transacting market, in
    /// the same currency. Must be non-negative; anything else fails the
    /// submission closed. Throw on unavailability — a submission never
    /// proceeds with guessed tax (SECURITY.md, Logging rule 2).
    /// </summary>
    Money CalculateTax(Market market, Money taxableTotal);
}
