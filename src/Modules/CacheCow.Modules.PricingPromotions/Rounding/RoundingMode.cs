namespace CacheCow.Modules.PricingPromotions.Rounding;

/// <summary>
/// Explicit rounding modes for scaling integer minor units (percentage
/// discounts, DE unit price per kilogram). The specs ratify no rounding policy
/// (issues 002/033/034, Open Questions), so the zero value is deliberately
/// unassigned: every money-scaling operation requires a caller-supplied,
/// configuration-ratified mode, and an unspecified (default) value is rejected
/// rather than silently interpreted (CC-PRC-003 fail-closed discipline).
/// AWAITING HUMAN RATIFICATION: which mode each money path uses.
/// </summary>
public enum RoundingMode
{
    // 0 intentionally unassigned — "no policy chosen" must never be a valid policy.

    /// <summary>Round to nearest; ties go away from zero (commercial rounding).</summary>
    HalfAwayFromZero = 1,

    /// <summary>Round to nearest; ties go to the even neighbor (banker's rounding).</summary>
    HalfToEven = 2,

    /// <summary>Always round toward zero (truncate).</summary>
    TowardZero = 3,

    /// <summary>Always round away from zero.</summary>
    AwayFromZero = 4,
}
