namespace CacheCow.Modules.Fulfillment.Routing;

/// <summary>
/// Why routing produced no assignment. Server-side detail for structured
/// logging and retry handling; client responses stay generic (SECURITY.md,
/// Logging rules 1 and 3). There is deliberately no default-region fallback
/// reason — unroutable orders remain unrouted (issue 044, Failure Behavior).
/// </summary>
public enum RoutingFailureReason
{
    None = 0,

    /// <summary>No serving-region data exists for the transacting market.</summary>
    MarketNotServed,

    /// <summary>No regional cold store serves the delivery postal code.</summary>
    PostalCodeNotServed,

    /// <summary>The serving-region lookup failed; fail closed, never a default assignment (SECURITY.md, Logging rule 2).</summary>
    ResolutionFailed,
}
