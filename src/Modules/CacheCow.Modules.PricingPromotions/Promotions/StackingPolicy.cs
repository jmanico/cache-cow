namespace CacheCow.Modules.PricingPromotions.Promotions;

/// <summary>
/// Promotion stacking rule (CC-PRC-006: "stacking rules (default: no
/// stacking)"). Only the default is ratified: the permitted non-default
/// combinations, precedence, and percentage-vs-fixed ordering are unspecified
/// (issue 033, Open Questions), so no other value exists — non-default stacking
/// is unrepresentable until a human ratifies its semantics. FLAGGED, not resolved.
/// </summary>
public enum StackingPolicy
{
    /// <summary>At most one promotion applies to a line (the ratified default, CC-PRC-006).</summary>
    NoStacking = 0,
}
