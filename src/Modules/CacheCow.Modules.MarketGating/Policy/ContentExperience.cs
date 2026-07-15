namespace CacheCow.Modules.MarketGating.Policy;

/// <summary>
/// The market-gated content experiences named by CC-MKT-005. Availability and
/// navigation placement are per-market policy data (CC-MKT-006), never
/// conditionals in consuming modules.
/// </summary>
public enum ContentExperience
{
    /// <summary>
    /// "Meet our Cuts": interactive butcher diagram (CC-CNT-003). MUST NOT
    /// render, link, or be reachable in the IN market (CC-MKT-005).
    /// </summary>
    MeetOurCuts = 0,

    /// <summary>
    /// "Meet our Cows": mascot/herd content (CC-CNT-002). Primary navigation in
    /// the IN market; under Our Story in all other markets (CC-MKT-005).
    /// </summary>
    MeetOurCows = 1,
}
