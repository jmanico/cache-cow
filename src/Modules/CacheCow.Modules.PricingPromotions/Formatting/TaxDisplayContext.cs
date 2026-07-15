namespace CacheCow.Modules.PricingPromotions.Formatting;

/// <summary>
/// The market display-convention flags a caller resolved from gating policy
/// (CC-PRC-002) and passes into formatting as input: tax inclusive/exclusive
/// presentation, plus whether a unit price per kilogram must accompany every
/// price (DE, Preisangabenverordnung). Consumed data only — this module never
/// derives the convention from locale or client hints (CC-SEC-012).
/// </summary>
public sealed record TaxDisplayContext
{
    public TaxDisplayContext(TaxPresentation presentation, bool displayUnitPricePerKilogram)
    {
        if (!Enum.IsDefined(presentation))
        {
            throw new PricingValidationException(
                "The tax presentation convention is unset or unknown; refusing to display a price under an unresolved legal convention (CC-PRC-002; fail closed).");
        }

        Presentation = presentation;
        DisplayUnitPricePerKilogram = displayUnitPricePerKilogram;
    }

    public TaxPresentation Presentation { get; }

    /// <summary>True when the market mandates €/kg alongside every price (DE per CC-PRC-002).</summary>
    public bool DisplayUnitPricePerKilogram { get; }
}
