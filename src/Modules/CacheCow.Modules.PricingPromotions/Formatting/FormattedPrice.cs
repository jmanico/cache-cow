namespace CacheCow.Modules.PricingPromotions.Formatting;

/// <summary>
/// Structured formatting result (issue 034): the locale-formatted amount, the
/// locale-formatted unit price per kilogram when the market convention requires
/// one (DE, CC-PRC-002), and which note keys apply. Deliberately not a
/// translated sentence — localized wording belongs to the Content &amp;
/// Localization context (CC-I18N-002).
/// </summary>
public sealed record FormattedPrice(
    string Amount,
    string? UnitPricePerKilogramAmount,
    IReadOnlyList<PriceDisplayNote> Notes);
