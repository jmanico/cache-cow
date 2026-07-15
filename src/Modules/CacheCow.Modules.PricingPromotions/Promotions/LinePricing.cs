using CacheCow.SharedKernel;

namespace CacheCow.Modules.PricingPromotions.Promotions;

/// <summary>
/// Evaluated pricing for one line: subtotal (quantity × unit price), the single
/// applied discount under the no-stacking rule (CC-PRC-006), and the resulting
/// total — all overflow-checked integer minor units, never negative
/// (CC-PRC-003; issue 033 AC-05). <paramref name="AppliedPromotionId"/> is the
/// neutral engine identifier, never presentation naming (CC-PRC-007).
/// </summary>
public sealed record LinePricing(
    SkuId Sku,
    long Quantity,
    Money Subtotal,
    Money Discount,
    Money Total,
    string? AppliedPromotionId);
