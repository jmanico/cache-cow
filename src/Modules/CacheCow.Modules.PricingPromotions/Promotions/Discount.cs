using CacheCow.SharedKernel;

namespace CacheCow.Modules.PricingPromotions.Promotions;

/// <summary>
/// A promotion's discount: percentage or fixed amount (CC-PRC-006). The
/// hierarchy is closed (private-protected constructor) so evaluation can never
/// meet an unknown discount kind.
/// </summary>
public abstract record Discount
{
    private protected Discount()
    {
    }
}

/// <summary>
/// A percentage discount expressed in basis points (1500 = 15.00%) — exact
/// integers, never binary floating point (CC-PRC-003). Applying it to integer
/// minor units requires an explicit rounding mode at evaluation; no rounding
/// policy is ratified (issue 033, Open Questions).
/// </summary>
public sealed record PercentageDiscount : Discount
{
    public PercentageDiscount(long basisPoints)
    {
        if (basisPoints is <= 0 or > 10_000)
        {
            throw new PricingValidationException(
                "A percentage discount must be between 1 and 10000 basis points (0% exclusive to 100% inclusive; CC-PRC-006).");
        }

        BasisPoints = basisPoints;
    }

    /// <summary>Discount rate in basis points of the line subtotal (10000 = 100%).</summary>
    public long BasisPoints { get; }
}

/// <summary>
/// A fixed amount off each unit, in the promotion market's currency. The specs
/// say only "fixed discounts" (CC-PRC-006) without defining the base
/// (per unit / per line / per order); this type makes the implemented semantics
/// explicit in its name, and other bases stay unrepresentable pending
/// ratification (issue 033, Open Questions — flagged, not resolved).
/// </summary>
public sealed record FixedAmountPerUnitDiscount : Discount
{
    public FixedAmountPerUnitDiscount(Money amountPerUnit)
    {
        if (amountPerUnit.MinorUnits <= 0)
        {
            throw new PricingValidationException(
                "A fixed discount must be a positive amount of minor units (CC-PRC-006).");
        }

        AmountPerUnit = amountPerUnit;
    }

    /// <summary>Amount deducted per unit, integer minor units (CC-PRC-003).</summary>
    public Money AmountPerUnit { get; }
}
