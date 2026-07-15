namespace CacheCow.Modules.Fulfillment.Routing;

/// <summary>
/// Outcome of an attempted cross-region override (CC-FUL-001). Applied results
/// exist only after the audit event was appended (issue 044, AC-03/AC-07).
/// </summary>
public sealed record OverrideResult
{
    private OverrideResult(ColdStoreAssignment? assignment, OverrideDenialReason denial)
    {
        Assignment = assignment;
        Denial = denial;
    }

    /// <summary>The re-routed assignment; null when denied.</summary>
    public ColdStoreAssignment? Assignment { get; }

    /// <summary><see cref="OverrideDenialReason.None"/> when applied.</summary>
    public OverrideDenialReason Denial { get; }

    public bool IsApplied => Assignment is not null;

    public static OverrideResult Applied(ColdStoreAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        return new OverrideResult(assignment, OverrideDenialReason.None);
    }

    public static OverrideResult Denied(OverrideDenialReason denial) =>
        denial == OverrideDenialReason.None
            ? throw new ArgumentOutOfRangeException(nameof(denial), "A denied result requires a reason.")
            : new OverrideResult(null, denial);
}
