using CacheCow.Modules.BackOffice;
using CacheCow.Modules.BackOffice.Rbac;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.BackOffice.Tests;

/// <summary>
/// Issue 080: the role–permission matrix is validated for closure at load —
/// unknown role or permission names reject the whole configuration — and
/// grants are exact memberships with no wildcard or implicit-admin logic
/// (CC-DSH-002; SECURITY.md, Authentication rule 8; Input validation rule 1).
/// The matrix CONTENT used here is test data only; the real content needs
/// human authoring (issue 080, Open Questions).
/// </summary>
public sealed class RolePermissionMatrixTests
{
    private static Dictionary<string, IReadOnlyCollection<string>> ValidGrants() =>
        new(StringComparer.Ordinal)
        {
            ["sales-viewer"] = ["analytics.view"],
            ["ops-agent"] = ["orders.search", "orders.transition"],
            ["hr-admin"] = ["employees.manage"],
        };

    [Fact]
    [Requirement("CC-DSH-002")]
    public void Valid_configuration_loads_and_answers_exact_grants()
    {
        var matrix = RolePermissionMatrix.Create(ValidGrants());

        Assert.True(matrix.IsGranted(StaffRole.SalesViewer, DashboardPermission.ViewSalesAnalytics));
        Assert.True(matrix.IsGranted(StaffRole.OpsAgent, DashboardPermission.SearchOrders));
        Assert.False(matrix.IsGranted(StaffRole.SalesViewer, DashboardPermission.SearchOrders));
        Assert.False(matrix.IsGranted(StaffRole.OpsAgent, DashboardPermission.ViewSalesAnalytics));
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    public void Unknown_role_name_in_configuration_is_rejected_at_load()
    {
        var grants = ValidGrants();
        grants["superuser"] = ["orders.search"];

        var exception = Assert.Throws<RbacConfigurationException>(() => RolePermissionMatrix.Create(grants));
        Assert.Contains("superuser", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    public void Unknown_permission_name_in_configuration_is_rejected_at_load()
    {
        var grants = ValidGrants();
        grants["finance"] = ["invoices.manage", "orders.delete-all"];

        var exception = Assert.Throws<RbacConfigurationException>(() => RolePermissionMatrix.Create(grants));
        Assert.Contains("orders.delete-all", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    public void Case_mismatched_names_are_unknown_not_normalized()
    {
        var grants = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
        {
            ["Admin"] = ["orders.search"],
        };

        Assert.Throws<RbacConfigurationException>(() => RolePermissionMatrix.Create(grants));
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    public void Null_permission_list_is_rejected_at_load()
    {
        var grants = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
        {
            ["finance"] = null!,
        };

        Assert.Throws<RbacConfigurationException>(() => RolePermissionMatrix.Create(grants));
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    public void Roles_absent_from_the_configuration_hold_nothing()
    {
        var matrix = RolePermissionMatrix.Create(ValidGrants());

        foreach (var permission in DashboardPermission.All)
        {
            Assert.False(matrix.IsGranted(StaffRole.Finance, permission));
            Assert.False(matrix.IsGranted(StaffRole.Admin, permission));
        }
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    public void Admin_holds_only_explicit_grants_no_wildcard_or_implicit_logic()
    {
        // admin is granted exactly one permission; least privilege demands it
        // hold that one and nothing else (SECURITY.md, Authentication rule 8).
        var grants = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
        {
            ["admin"] = ["staff-roles.change"],
        };
        var matrix = RolePermissionMatrix.Create(grants);

        foreach (var permission in DashboardPermission.All)
        {
            Assert.Equal(
                permission == DashboardPermission.ChangeStaffRoles,
                matrix.IsGranted(StaffRole.Admin, permission));
        }

        Assert.Equal([DashboardPermission.ChangeStaffRoles], matrix.GrantsFor(StaffRole.Admin));
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    public void GrantsFor_documents_the_exact_configured_memberships()
    {
        var matrix = RolePermissionMatrix.Create(ValidGrants());

        var opsGrants = matrix.GrantsFor(StaffRole.OpsAgent)
            .Select(p => p.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["orders.search", "orders.transition"], opsGrants);
        Assert.Empty(matrix.GrantsFor(StaffRole.Finance));
    }
}
