using System.Diagnostics.CodeAnalysis;

namespace CacheCow.Modules.BackOffice.Rbac;

/// <summary>
/// The closed set of dashboard staff roles (CC-DSH-002): sales-viewer,
/// ops-agent, finance, hr-admin, admin. The constructor is private, so no
/// role outside this set is representable; role names arriving from session
/// claims resolve through <see cref="TryResolve"/>, and anything that does
/// not resolve is denied by <see cref="DashboardAuthorizationService"/>
/// (fail closed, SECURITY.md, Logging rule 2).
///
/// Whether roles are mutually exclusive or combinable per staff member is an
/// open question (issue 080, Open Questions) — this type models single roles
/// and does not resolve it.
/// </summary>
public sealed class StaffRole
{
    /// <summary>Read-only sales analytics staff (CC-DSH-002).</summary>
    public static readonly StaffRole SalesViewer = new("sales-viewer");

    /// <summary>Operations agents handling orders and fulfillment (CC-DSH-002).</summary>
    public static readonly StaffRole OpsAgent = new("ops-agent");

    /// <summary>Finance staff (CC-DSH-002).</summary>
    public static readonly StaffRole Finance = new("finance");

    /// <summary>HR administrators — the only role the CC-DSH-005 pointer permits near employee records (issue 087).</summary>
    public static readonly StaffRole HrAdmin = new("hr-admin");

    /// <summary>
    /// Platform administrators (CC-DSH-002). Holding this role grants
    /// NOTHING by itself: every permission — admin's included — comes only
    /// from an explicit role–permission matrix entry (least privilege,
    /// SECURITY.md, Authentication rule 8; no wildcard or implicit grants).
    /// </summary>
    public static readonly StaffRole Admin = new("admin");

    private static readonly Dictionary<string, StaffRole> ByName =
        new(StringComparer.Ordinal)
        {
            [SalesViewer.Name] = SalesViewer,
            [OpsAgent.Name] = OpsAgent,
            [Finance.Name] = Finance,
            [HrAdmin.Name] = HrAdmin,
            [Admin.Name] = Admin,
        };

    private StaffRole(string name)
    {
        Name = name;
    }

    /// <summary>The five minimum launch roles, exactly (CC-DSH-002).</summary>
    public static IReadOnlyCollection<StaffRole> All => ByName.Values;

    /// <summary>The canonical role name as it appears in the matrix and session claims.</summary>
    public string Name { get; }

    /// <summary>
    /// Fail-closed resolution: exact (ordinal, case-sensitive) match against
    /// the closed set only. Unknown, differently-cased, or padded names do
    /// not resolve, and callers treat non-resolution as denial (issue 080,
    /// Failure Behavior: malformed or unknown role claims are rejected).
    /// </summary>
    public static bool TryResolve(string? name, [NotNullWhen(true)] out StaffRole? role)
    {
        if (name is null)
        {
            role = null;
            return false;
        }

        return ByName.TryGetValue(name, out role);
    }

    public override string ToString() => Name;
}
