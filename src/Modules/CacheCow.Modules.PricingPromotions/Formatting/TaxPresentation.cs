namespace CacheCow.Modules.PricingPromotions.Formatting;

/// <summary>
/// Which tax-display convention governs a price (CC-PRC-002). The convention
/// itself is per-market gating-policy data owned by the Market &amp; Gating
/// Policy context; this module only consumes it as input. The zero value is
/// deliberately unassigned so an unset convention fails closed instead of
/// silently displaying with the wrong legal convention (issue 034, Failure
/// Behavior).
/// </summary>
public enum TaxPresentation
{
    // 0 intentionally unassigned — an unresolved convention must never display.

    /// <summary>Displayed price is the tax-inclusive amount (DE/ES/MX/JP/IN per CC-PRC-002).</summary>
    TaxInclusive = 1,

    /// <summary>Displayed price is tax-exclusive; estimated tax is computed at checkout by the order path (US per CC-PRC-002).</summary>
    TaxExclusiveEstimatedAtCheckout = 2,
}
