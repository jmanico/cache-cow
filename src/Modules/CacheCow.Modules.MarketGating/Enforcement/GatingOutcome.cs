namespace CacheCow.Modules.MarketGating.Enforcement;

/// <summary>
/// The two possible gating outcomes. The zero value is the exclusion so a
/// defaulted decision denies (fail closed, SECURITY.md Logging rule 2).
/// </summary>
public enum GatingOutcome
{
    /// <summary>
    /// Excluded for this market: present to HTTP as 404 — never 403, never a
    /// redirect to another market (CC-MKT-004; SECURITY.md, Authentication
    /// rule 9). Indistinguishable from a genuinely nonexistent resource.
    /// </summary>
    ExcludedPresentAsNotFound = 0,

    /// <summary>Allowed for this market's responses.</summary>
    Allowed = 1,
}
