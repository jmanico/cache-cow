using CacheCow.SharedKernel;

namespace CacheCow.Modules.PricingPromotions.Rounding;

/// <summary>
/// Exact integer scaling of minor-unit amounts (CC-PRC-003): the intermediate
/// product is computed in 128-bit integer arithmetic (never binary floating
/// point), the remainder is resolved by an explicit <see cref="RoundingMode"/>,
/// and results that exceed the representable range fail closed rather than
/// wrapping (relevant for attacker-influenced quantities and large-grouping
/// currencies per CC-PRC-003/CC-QA-004).
/// </summary>
public static class MinorUnitArithmetic
{
    /// <summary>
    /// Computes value × multiplier ÷ divisor exactly, rounding the quotient per
    /// <paramref name="rounding"/>. Only non-negative scaling exists on this
    /// context's money paths (discounts, unit-price derivation).
    /// </summary>
    public static long MultiplyAndDivide(long value, long multiplier, long divisor, RoundingMode rounding)
    {
        RequireSpecified(rounding);

        if (value < 0 || multiplier < 0)
        {
            throw new PricingValidationException(
                "Minor-unit scaling is defined for non-negative values only on this money path (CC-PRC-003).");
        }

        if (divisor <= 0)
        {
            throw new PricingValidationException(
                "Minor-unit scaling requires a positive divisor (CC-PRC-003).");
        }

        var numerator = (Int128)value * multiplier;
        var quotient = numerator / divisor;
        var remainder = numerator % divisor;

        if (remainder != 0)
        {
            var twiceRemainder = remainder * 2;
            var roundUp = rounding switch
            {
                RoundingMode.TowardZero => false,
                RoundingMode.AwayFromZero => true,
                RoundingMode.HalfAwayFromZero => twiceRemainder >= divisor,
                RoundingMode.HalfToEven => twiceRemainder > divisor
                    || (twiceRemainder == divisor && (quotient & 1) == 1),
                _ => throw new PricingValidationException(
                    "Unrecognized rounding mode; failing closed (CC-PRC-003)."),
            };

            if (roundUp)
            {
                quotient += 1;
            }
        }

        if (quotient > long.MaxValue)
        {
            throw new MoneyOverflowException("minor-unit scaling");
        }

        return (long)quotient;
    }

    /// <summary>
    /// Rejects the unassigned zero value and any undefined value: no rounding
    /// policy is ratified in the specs (issues 002/033/034, Open Questions), so
    /// "unspecified" is never silently interpreted as a mode.
    /// </summary>
    public static void RequireSpecified(RoundingMode rounding)
    {
        if (!Enum.IsDefined(rounding))
        {
            throw new PricingValidationException(
                "No rounding mode was specified. The rounding policy for money scaling is not ratified (issues 002/033/034, Open Questions); callers must pass an explicit, configuration-supplied RoundingMode. AWAITING HUMAN RATIFICATION.");
        }
    }
}
