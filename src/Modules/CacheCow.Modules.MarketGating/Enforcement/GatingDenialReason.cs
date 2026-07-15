namespace CacheCow.Modules.MarketGating.Enforcement;

/// <summary>
/// Why a resource was excluded — for structured security/gating logs only
/// (SECURITY.md, Logging rule 3). Never surfaces to a client: the client-facing
/// presentation of every exclusion is the indistinguishable 404
/// (CC-MKT-004; SECURITY.md, Logging rule 1).
/// </summary>
public enum GatingDenialReason
{
    None = 0,

    /// <summary>Non-veg SKU in a market whose policy excludes non-veg (IN, CC-MKT-003).</summary>
    NonVegExcludedFromMarket = 1,

    /// <summary>Content experience not available in this market (Cuts in IN, CC-MKT-005).</summary>
    ContentExperienceUnavailableInMarket = 2,

    /// <summary>No policy exists for the market — fail closed, never an ungated default (issue 023 AC-05).</summary>
    UnknownMarketPolicy = 3,

    /// <summary>SKU classification outside the declared set — fail closed (SECURITY.md, Logging rule 2).</summary>
    UnknownSkuClassification = 4,

    /// <summary>Response surface outside the declared set — fail closed.</summary>
    UnknownResponseSurface = 5,

    /// <summary>Content experience outside the declared set — fail closed.</summary>
    UnknownContentExperience = 6,

    /// <summary>Missing/invalid gating input — fail closed (SECURITY.md, Logging rule 2).</summary>
    MissingGatingInput = 7,
}
