namespace CacheCow.Modules.Fulfillment.Routing;

/// <summary>
/// Why a cross-region override was denied. Server-side detail for security
/// logging and alerting (SECURITY.md, Logging rule 3); client responses stay
/// generic (SECURITY.md, Logging rule 1).
/// </summary>
public enum OverrideDenialReason
{
    None = 0,

    /// <summary>The target store does not exist in the serving-region data; fail closed (issue 044, Failure Behavior).</summary>
    UnknownTargetStore,

    /// <summary>The audit event could not be appended, so the override does not take effect (issue 044, AC-07; SECURITY.md, Logging rule 2).</summary>
    AuditAppendFailed,

    /// <summary>The override evaluation itself failed; denial, never a bypass (SECURITY.md, Logging rule 2).</summary>
    EvaluationFailed,
}
