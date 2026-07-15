namespace CacheCow.Modules.MarketGating.Enforcement;

/// <summary>
/// The result of a gating evaluation. The 404 semantics of CC-MKT-004 are
/// encoded as data on the decision so HTTP layers cannot get them wrong: an
/// excluded resource carries exactly <see cref="NotFoundStatusCode"/> — never
/// 403, never a redirect (there is no redirect target on this type, by
/// construction) — making a gated resource indistinguishable from a
/// nonexistent one (SECURITY.md, Authentication rule 9; CWE-204).
/// </summary>
public sealed record GatingDecision
{
    /// <summary>The only status code a gated exclusion may present (CC-MKT-004).</summary>
    public const int NotFoundStatusCode = 404;

    private GatingDecision(GatingOutcome outcome, GatingDenialReason denialReason)
    {
        Outcome = outcome;
        DenialReason = denialReason;
    }

    /// <summary>The single allowed-decision instance.</summary>
    public static GatingDecision Allowed { get; } = new(GatingOutcome.Allowed, GatingDenialReason.None);

    public GatingOutcome Outcome { get; }

    /// <summary>Server-side log detail only — never surfaced to clients (SECURITY.md, Logging rule 1).</summary>
    public GatingDenialReason DenialReason { get; }

    public bool IsAllowed => Outcome == GatingOutcome.Allowed;

    /// <summary>
    /// 404 when excluded, null when allowed. There is deliberately no way to
    /// express 403 or a redirect on this type (CC-MKT-004).
    /// </summary>
    public int? ExcludedHttpStatusCode => IsAllowed ? null : NotFoundStatusCode;

    public static GatingDecision ExcludedAsNotFound(GatingDenialReason denialReason) =>
        new(GatingOutcome.ExcludedPresentAsNotFound, denialReason);
}
