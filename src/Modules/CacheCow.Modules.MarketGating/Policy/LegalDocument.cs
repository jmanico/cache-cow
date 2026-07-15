namespace CacheCow.Modules.MarketGating.Policy;

/// <summary>
/// The per-market legal content set (CC-CNT-005): privacy policy, terms, and
/// shipping/returns everywhere; DE additionally Impressum and
/// Widerrufsbelehrung. The documents' text lives in the Content &amp;
/// Localization context; which set a market requires is gating policy data.
/// </summary>
public enum LegalDocument
{
    PrivacyPolicy = 0,
    Terms = 1,
    ShippingAndReturns = 2,

    /// <summary>DE only (CC-CNT-005; DESIGN.md §8.4).</summary>
    Impressum = 3,

    /// <summary>DE only (CC-CNT-005; CC-FUL-003 governs the perishable-goods exception text).</summary>
    Widerrufsbelehrung = 4,
}
