using CacheCow.SharedKernel;

namespace CacheCow.Modules.PricingPromotions.Promotions;

/// <summary>
/// Result of authoritative promotion evaluation for one market at one instant
/// (CC-PRC-006). Side-effect-free output the order service uses as final
/// authority at submission (ARCHITECTURE.md, "Server bounded contexts" 3).
/// </summary>
public sealed record PromotionEvaluationResult(
    Market Market,
    IReadOnlyList<LinePricing> Lines,
    Money Total);
