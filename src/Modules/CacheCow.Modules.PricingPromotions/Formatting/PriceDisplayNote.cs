namespace CacheCow.Modules.PricingPromotions.Formatting;

/// <summary>
/// Structured keys for the auxiliary notes a price display must carry
/// (CC-PRC-002; DESIGN.md §7 "always includes the market's tax-inclusion
/// note"). Keys only: the localized wording (e.g. "incl. VAT") is ICU
/// MessageFormat string-resource content owned by the Content &amp;
/// Localization context (CC-I18N-002), never emitted from this module.
/// </summary>
public enum PriceDisplayNote
{
    // 0 intentionally unassigned.

    /// <summary>The displayed amount includes tax (DE/ES/MX/JP/IN per CC-PRC-002).</summary>
    TaxInclusive = 1,

    /// <summary>The displayed amount excludes tax; estimated tax appears at checkout (US per CC-PRC-002).</summary>
    TaxExclusiveEstimatedTaxAtCheckout = 2,

    /// <summary>A unit price per kilogram accompanies the price (DE, Preisangabenverordnung, CC-PRC-002).</summary>
    UnitPricePerKilogram = 3,
}
