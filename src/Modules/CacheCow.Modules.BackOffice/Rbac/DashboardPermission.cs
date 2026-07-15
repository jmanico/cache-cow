using System.Diagnostics.CodeAnalysis;

namespace CacheCow.Modules.BackOffice.Rbac;

/// <summary>
/// The closed set of typed dashboard permission identifiers, derived from the
/// launch dashboard modules (CC-DSH-003: sales analytics, order management —
/// search, state transitions, refunds — invoice management, inventory by cold
/// store, partner management, employee management) plus the audited
/// cross-region fulfillment override (CC-FUL-001) and staff role changes
/// (SECURITY.md, Authentication rule 2). The constructor is private, so no
/// permission outside this set is representable, and the role–permission
/// matrix can only reference these identifiers (CC-DSH-002).
///
/// <see cref="RequiresRecentReauth"/> marks the sensitive actions SECURITY.md,
/// Authentication rule 2 names — refunds, employee-record access, role
/// changes — which require step-up re-authentication before they are granted.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "This is an RBAC permission in the CC-DSH-002 sense — the term the specs use throughout — not a System.Security.IPermission.")]
public sealed class DashboardPermission
{
    /// <summary>View sales analytics by market/SKU/channel (CC-DSH-003, CC-DSH-006).</summary>
    public static readonly DashboardPermission ViewSalesAnalytics = new("analytics.view", requiresRecentReauth: false);

    /// <summary>Search orders in order management (CC-DSH-003).</summary>
    public static readonly DashboardPermission SearchOrders = new("orders.search", requiresRecentReauth: false);

    /// <summary>Perform order state transitions (CC-DSH-003, CC-ORD-006).</summary>
    public static readonly DashboardPermission TransitionOrders = new("orders.transition", requiresRecentReauth: false);

    /// <summary>Issue refunds — sensitive, requires recent re-authentication (SECURITY.md, Authentication rule 2).</summary>
    public static readonly DashboardPermission IssueRefunds = new("orders.refund", requiresRecentReauth: true);

    /// <summary>Manage invoices (CC-DSH-003, CC-INV-001).</summary>
    public static readonly DashboardPermission ManageInvoices = new("invoices.manage", requiresRecentReauth: false);

    /// <summary>View inventory by regional cold store (CC-DSH-003, CC-CAT-002).</summary>
    public static readonly DashboardPermission ViewInventory = new("inventory.view", requiresRecentReauth: false);

    /// <summary>Manage wholesale partners (CC-DSH-003, CC-WHS-002).</summary>
    public static readonly DashboardPermission ManagePartners = new("partners.manage", requiresRecentReauth: false);

    /// <summary>Approve partner onboarding — the no-self-service activation gate (CC-WHS-002; issue 049).</summary>
    public static readonly DashboardPermission ApprovePartners = new("partners.approve", requiresRecentReauth: false);

    /// <summary>
    /// Employee management / employee-record access — sensitive, requires
    /// recent re-authentication (SECURITY.md, Authentication rule 2). The
    /// hr-admin-only restriction and field-level protections are CC-DSH-005
    /// (issue 087), built on this matrix entry.
    /// </summary>
    public static readonly DashboardPermission ManageEmployees = new("employees.manage", requiresRecentReauth: true);

    /// <summary>Change staff role assignments — sensitive, requires recent re-authentication (SECURITY.md, Authentication rule 2); role changes are audited privileged actions (CC-DSH-004).</summary>
    public static readonly DashboardPermission ChangeStaffRoles = new("staff-roles.change", requiresRecentReauth: true);

    /// <summary>Apply the audited cross-region fulfillment override (CC-FUL-001; issue 044).</summary>
    public static readonly DashboardPermission OverrideCrossRegionFulfillment = new("fulfillment.cross-region-override", requiresRecentReauth: false);

    private static readonly Dictionary<string, DashboardPermission> ByName =
        new(StringComparer.Ordinal)
        {
            [ViewSalesAnalytics.Name] = ViewSalesAnalytics,
            [SearchOrders.Name] = SearchOrders,
            [TransitionOrders.Name] = TransitionOrders,
            [IssueRefunds.Name] = IssueRefunds,
            [ManageInvoices.Name] = ManageInvoices,
            [ViewInventory.Name] = ViewInventory,
            [ManagePartners.Name] = ManagePartners,
            [ApprovePartners.Name] = ApprovePartners,
            [ManageEmployees.Name] = ManageEmployees,
            [ChangeStaffRoles.Name] = ChangeStaffRoles,
            [OverrideCrossRegionFulfillment.Name] = OverrideCrossRegionFulfillment,
        };

    private DashboardPermission(string name, bool requiresRecentReauth)
    {
        Name = name;
        RequiresRecentReauth = requiresRecentReauth;
    }

    /// <summary>Every dashboard permission in the closed set.</summary>
    public static IReadOnlyCollection<DashboardPermission> All => ByName.Values;

    /// <summary>The canonical permission identifier as it appears in the matrix.</summary>
    public string Name { get; }

    /// <summary>
    /// True for the sensitive actions of SECURITY.md, Authentication rule 2
    /// (refunds, employee-record access, role changes): the grant additionally
    /// requires re-authentication within the configured step-up max age.
    /// </summary>
    public bool RequiresRecentReauth { get; }

    /// <summary>
    /// Fail-closed resolution: exact (ordinal) match against the closed set
    /// only. Unknown permission names do not resolve; matrix loading rejects
    /// them outright (CC-DSH-002; SECURITY.md, Input validation rule 1).
    /// </summary>
    public static bool TryResolve(string? name, [NotNullWhen(true)] out DashboardPermission? permission)
    {
        if (name is null)
        {
            permission = null;
            return false;
        }

        return ByName.TryGetValue(name, out permission);
    }

    public override string ToString() => Name;
}
