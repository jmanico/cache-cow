namespace CacheCow.Modules.WholesaleB2B.Partners;

/// <summary>
/// Partner tenancy workflow states (CC-WHS-002). Only <see cref="Approved"/>
/// is active: every other state is "non-active pending" in the sense of
/// issue 049 AC-01 — no wholesale price list, order capability, portal access,
/// or B2B API authorization is reachable from it (enforced by
/// <see cref="PartnerTenantContext"/>, which only exists for approved tenants).
/// </summary>
public enum PartnerOnboardingState
{
    /// <summary>Record created in the dashboard; identity capture in progress.</summary>
    Draft = 0,

    /// <summary>Submitted for approval — the pending state of issue 049 AC-01.</summary>
    Submitted = 1,

    /// <summary>The single active state; reached only via the audited approval action (issue 049 AC-02).</summary>
    Approved = 2,

    /// <summary>Approval declined. Terminal: no resubmission path is specified (issue 049, Open Questions).</summary>
    Rejected = 3,

    /// <summary>Deactivated after approval. Terminal until a reinstatement decision is ratified (fail closed).</summary>
    Suspended = 4,
}
