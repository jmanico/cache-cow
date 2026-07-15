namespace CacheCow.Modules.Fulfillment.Serviceability;

/// <summary>
/// Machine-readable reason an order submission is not serviceable
/// (CC-FUL-002). Checkout maps these to RFC 9457 problem details naming the
/// failed constraint in generic, actionable terms — no internal carrier or
/// topology detail crosses the boundary (issue 045, Failure Behavior;
/// SECURITY.md, Logging rule 1).
/// </summary>
public enum ServiceabilityDenialReason
{
    None = 0,

    /// <summary>The postal code is not on the transacting market's serviceable set — including unknown codes (fail closed).</summary>
    PostalCodeNotServiceable,

    /// <summary>No regional cold store serves the address, so there is no transit origin (CC-FUL-001 routing precondition).</summary>
    NoServingColdStore,

    /// <summary>No carrier transit estimate is available; fail closed, never optimistic (issue 045, Failure Behavior).</summary>
    TransitEstimateUnavailable,

    /// <summary>Every carrier option exceeds the 48-hour frozen transit maximum (CC-FUL-002, ratified 2026-07-15).</summary>
    TransitExceedsFrozenLimit,

    /// <summary>The serviceability evaluation itself failed; denial, never a bypass (SECURITY.md, Logging rule 2).</summary>
    EvaluationFailed,
}
