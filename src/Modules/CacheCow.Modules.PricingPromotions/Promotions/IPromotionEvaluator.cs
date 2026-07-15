namespace CacheCow.Modules.PricingPromotions.Promotions;

/// <summary>
/// The idempotent, side-effect-free promotion evaluation contract (CC-PRC-006).
/// The order service calls it authoritatively at submission; storefront display
/// state and cached UI are never inputs (ARCHITECTURE.md, Dependency rule 2).
/// </summary>
public interface IPromotionEvaluator
{
    /// <summary>
    /// Evaluates the request against the injected authoritative clock: expired
    /// or not-yet-started promotions never apply regardless of what cached UI
    /// displayed (CC-PRC-006), windows are interpreted in the market's
    /// configured timezone, and at most one promotion applies per line
    /// (no stacking). Any failure is a denial — it never grants a discount
    /// (SECURITY.md, Logging rule 2).
    /// </summary>
    PromotionEvaluationResult Evaluate(PromotionEvaluationRequest request);
}
