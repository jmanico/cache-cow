namespace CacheCow.Modules.BackOffice.Auditing;

/// <summary>
/// Retention marker on every audit event (CC-DSH-004; CC-CMP-003).
/// </summary>
public enum AuditRetentionClass
{
    /// <summary>
    /// Non-financial audit event. Its retention period belongs to the
    /// CC-CMP-003 retention schedule (issue 090) and is not decided here
    /// (issue 081, Open Questions).
    /// </summary>
    Standard = 0,

    /// <summary>
    /// Financial action: retained for the ratified 7-year window
    /// (<see cref="AuditRetention.RatifiedFinancialRetentionYears"/>,
    /// CC-DSH-004, ratified 2026-07-15) and replicated to retention-locked
    /// WORM storage (CC-SEC-020; SECURITY.md, Logging rule 6). A data-subject
    /// erasure request cannot mutate these records; the retained fields are
    /// the documented erasure exception (CC-CMP-003).
    /// </summary>
    Financial = 1,
}

/// <summary>Ratified audit retention constants (decision record 2026-07-15).</summary>
public static class AuditRetention
{
    /// <summary>
    /// Financial audit events are retained 7 years (CC-DSH-004, ratified
    /// 2026-07-15). Retention execution and WORM replication topology are the
    /// storage layer's (issues 015/090) and are blocked on the open WORM
    /// storage-service and residency decisions (issue 081, Open Questions;
    /// ARCHITECTURE.md, "Known unknowns").
    /// </summary>
    public const int RatifiedFinancialRetentionYears = 7;
}
