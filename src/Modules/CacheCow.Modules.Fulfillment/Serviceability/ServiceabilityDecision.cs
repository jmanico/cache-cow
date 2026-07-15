namespace CacheCow.Modules.Fulfillment.Serviceability;

/// <summary>
/// The typed serviceability decision checkout consumes at order submission
/// (CC-FUL-002). Client-side serviceability state is advisory only; this
/// server-side decision is the authority, exactly as the server recomputes
/// money (issue 045, AC-04/AC-07; SECURITY.md, Input validation rule 1).
/// </summary>
public sealed record ServiceabilityDecision
{
    private ServiceabilityDecision(
        ServiceabilityDenialReason denial,
        ColdStoreId? servingStore,
        TimeSpan? estimatedTransit)
    {
        Denial = denial;
        ServingStore = servingStore;
        EstimatedTransit = estimatedTransit;
    }

    public bool IsServiceable => Denial == ServiceabilityDenialReason.None;

    /// <summary><see cref="ServiceabilityDenialReason.None"/> when serviceable.</summary>
    public ServiceabilityDenialReason Denial { get; }

    /// <summary>The regional cold store the transit was evaluated from; null when denied before resolution.</summary>
    public ColdStoreId? ServingStore { get; }

    /// <summary>The carrier transit estimate that passed the 48-hour check; null when denied.</summary>
    public TimeSpan? EstimatedTransit { get; }

    /// <summary>
    /// Stable machine-readable code for problem-details payloads (RFC 9457;
    /// issue 045, AC-02) — constraint names only, no internal topology.
    /// </summary>
    public string Code => Denial switch
    {
        ServiceabilityDenialReason.None => "serviceable",
        ServiceabilityDenialReason.PostalCodeNotServiceable => "postal-code-not-serviceable",
        ServiceabilityDenialReason.NoServingColdStore => "no-serving-cold-store",
        ServiceabilityDenialReason.TransitEstimateUnavailable => "transit-estimate-unavailable",
        ServiceabilityDenialReason.TransitExceedsFrozenLimit => "transit-exceeds-frozen-limit",
        ServiceabilityDenialReason.EvaluationFailed => "serviceability-evaluation-failed",
        _ => "serviceability-evaluation-failed",
    };

    public static ServiceabilityDecision Serviceable(ColdStoreId servingStore, TimeSpan estimatedTransit) =>
        FrozenTransitConstraint.IsWithinLimit(estimatedTransit)
            ? new ServiceabilityDecision(ServiceabilityDenialReason.None, servingStore, estimatedTransit)
            : throw new ArgumentOutOfRangeException(
                nameof(estimatedTransit),
                "A serviceable decision cannot carry a transit beyond the frozen limit (CC-FUL-002).");

    public static ServiceabilityDecision Denied(ServiceabilityDenialReason denial) =>
        denial == ServiceabilityDenialReason.None
            ? throw new ArgumentOutOfRangeException(nameof(denial), "A denied decision requires a reason.")
            : new ServiceabilityDecision(denial, null, null);
}
