namespace CacheCow.Modules.MarketGating.Policy;

/// <summary>
/// Navigation placement of a content experience in a market (CC-MKT-005;
/// DESIGN.md §8.1). The zero value is <see cref="NotAvailable"/> so a
/// defaulted/unknown placement gates as unreachable (fail closed).
/// </summary>
public enum ContentPlacement
{
    /// <summary>Not rendered, linked, or reachable in this market (presents as HTTP 404, CC-MKT-004 semantics).</summary>
    NotAvailable = 0,

    /// <summary>Present in primary navigation (Meet our Cows in IN, CC-MKT-005).</summary>
    PrimaryNavigation = 1,

    /// <summary>Present under the Our Story section (Meet our Cows outside IN, CC-MKT-005).</summary>
    UnderOurStory = 2,

    /// <summary>Present as a standard page with no special navigation promotion.</summary>
    StandardPage = 3,
}
