namespace CacheCow.Modules.BackOffice.Dashboard;

/// <summary>
/// The named rate-limiter policies the dashboard endpoints attach as metadata
/// (SECURITY.md, HTTP boundary rule 7). Same contract split as the B2B API's
/// policy metadata (issue 056): the HOST owns the limiter middleware and the
/// 429 + Retry-After semantics (issue 019) and must register both policies by
/// these exact names, partitioned per authenticated staff member; this module
/// only declares which policy guards which endpoint class. No numeric budget
/// is ratified for staff traffic anywhere in the canonical documents — the
/// budgets are host configuration needing a human decision, deliberately not
/// guessed here (CLAUDE.md, working rules).
/// </summary>
public static class DashboardRateLimitPolicies
{
    /// <summary>Every dashboard endpoint: the default per-staff-member policy.</summary>
    public const string Staff = "dashboard-staff";

    /// <summary>
    /// State-changing dashboard actions (order transitions, refunds, partner
    /// workflow actions): stricter than <see cref="Staff"/> per SECURITY.md,
    /// HTTP boundary rule 7 (stricter limits on sensitive endpoints).
    /// Endpoint-closest metadata overrides the group policy on these routes.
    /// </summary>
    public const string StaffCommands = "dashboard-staff-commands";
}
