namespace CacheCow.Modules.Fulfillment.Serviceability;

/// <summary>
/// The cold-chain transit limit for frozen product: maximum 48 hours carrier
/// transit, ratified 2026-07-15 (CC-FUL-002; ARCHITECTURE.md decision record,
/// "Cold-chain shipping spec"). A transit estimate exactly at the maximum is
/// within the limit; anything beyond it is not serviceable.
/// </summary>
public static class FrozenTransitConstraint
{
    /// <summary>48 hours, ratified 2026-07-15 (CC-FUL-002).</summary>
    public static TimeSpan MaximumTransit { get; } = TimeSpan.FromHours(48);

    public static bool IsWithinLimit(TimeSpan transit) => transit <= MaximumTransit;
}
