namespace CacheCow.Modules.WholesaleB2B.Partners;

/// <summary>
/// The audit record emitted for every partner tenancy transition (CC-WHS-002,
/// AC-02; CC-DSH-004; SECURITY.md, Logging rule 6): actor, action, object
/// (partner id), before/after state, timestamp. The shape is deliberately
/// closed — no free-form payload field exists, so business identifiers,
/// credentials, or PII cannot ride along (SECURITY.md, Logging rule 4). The
/// timestamp is server-set by <see cref="PartnerOnboardingWorkflow"/>, never
/// caller-supplied (SECURITY.md, Input validation rule 3).
/// </summary>
public sealed record PartnerAuditEvent(
    string Actor,
    string ActorRole,
    string Action,
    PartnerId PartnerId,
    PartnerOnboardingState FromState,
    PartnerOnboardingState ToState,
    DateTimeOffset Timestamp);
