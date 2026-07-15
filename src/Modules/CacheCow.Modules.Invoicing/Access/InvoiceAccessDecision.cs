namespace CacheCow.Modules.Invoicing.Access;

/// <summary>
/// Why access was denied — for the structured security log ONLY (SECURITY.md,
/// Logging rule 3: authz denials are logged and alerted). The client-facing
/// response never varies by reason: every denial maps to a uniform HTTP 404
/// that does not confirm the invoice exists (SECURITY.md, Authentication
/// rule 9; issue 048, AC-04/AC-06).
/// </summary>
public enum InvoiceAccessDenialReason
{
    None = 0,

    /// <summary>Token missing, malformed, expired, revoked, or unknown — deliberately indistinguishable.</summary>
    CapabilityTokenInvalid = 1,

    /// <summary>A valid token bound to a different order than this invoice's.</summary>
    CapabilityTokenBoundToOtherOrder = 2,

    /// <summary>The session account does not own the invoice's order (object-level authorization).</summary>
    NotResourceOwner = 3,

    /// <summary>The authorizer cannot evaluate this request kind — denied, never passed through.</summary>
    UnsupportedRequestKind = 4,

    /// <summary>An exception occurred during evaluation; fail closed (SECURITY.md, Logging rule 2).</summary>
    EvaluationFault = 5,
}

/// <summary>
/// Outcome of an invoice access check. Immutable; a denial carries a reason
/// for the audit/security log but exposes nothing existence-confirming to the
/// client (uniform 404 — SECURITY.md, Authentication rule 9).
/// </summary>
public sealed class InvoiceAccessDecision
{
    private static readonly InvoiceAccessDecision GrantedInstance =
        new(isGranted: true, InvoiceAccessDenialReason.None);

    private InvoiceAccessDecision(bool isGranted, InvoiceAccessDenialReason denialReason)
    {
        IsGranted = isGranted;
        DenialReason = denialReason;
    }

    public bool IsGranted { get; }

    /// <summary>Log-only detail; never surfaced to the client (issue 048, AC-04).</summary>
    public InvoiceAccessDenialReason DenialReason { get; }

    public static InvoiceAccessDecision Granted() => GrantedInstance;

    public static InvoiceAccessDecision Denied(InvoiceAccessDenialReason reason) =>
        reason == InvoiceAccessDenialReason.None
            ? throw new InvalidOperationException("A denial requires a logged reason (SECURITY.md, Logging rule 3).")
            : new InvoiceAccessDecision(isGranted: false, reason);
}
