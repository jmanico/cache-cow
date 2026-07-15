namespace CacheCow.Modules.MarketGating.Policy;

/// <summary>
/// Which vegetarian marking scheme a market's product presentations use
/// (CC-CNT-006; DESIGN.md §3.3). The zero value is the regulatory FSSAI mark —
/// the strictest scheme — so a defaulted value never under-marks the IN market.
/// </summary>
public enum VegetarianMarking
{
    /// <summary>The FSSAI green-in-square regulatory vegetarian mark (IN, CC-CNT-006).</summary>
    FssaiRegulatoryMark = 0,

    /// <summary>The simplified cache.500 leaf-dot badge plus the word "Vegetarian" (all non-IN markets, DESIGN.md §3.3).</summary>
    LeafDotBadge = 1,
}
