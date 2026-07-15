using CacheCow.Modules.BackOffice.Rbac;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.BackOffice.Tests;

/// <summary>
/// Issue 080: typed permission identifiers form a closed set derived from the
/// CC-DSH-003 dashboard modules and referenced privileged actions, with the
/// step-up marker on exactly the sensitive actions SECURITY.md,
/// Authentication rule 2 names (refunds, employee-record access, role
/// changes).
/// </summary>
public sealed class DashboardPermissionTests
{
    [Fact]
    [Requirement("CC-DSH-002")]
    public void Permission_set_is_exactly_the_dashboard_capability_catalog()
    {
        var names = DashboardPermission.All.Select(p => p.Name).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(
            [
                "analytics.view",
                "employees.manage",
                "fulfillment.cross-region-override",
                "inventory.view",
                "invoices.manage",
                "orders.refund",
                "orders.search",
                "orders.transition",
                "partners.approve",
                "partners.manage",
                "staff-roles.change",
            ],
            names);
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    public void Exactly_the_rule_2_sensitive_actions_require_recent_reauth()
    {
        var sensitive = DashboardPermission.All
            .Where(p => p.RequiresRecentReauth)
            .Select(p => p.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Refunds, employee-record access, role changes (SECURITY.md,
        // Authentication rule 2) — no more, no fewer.
        Assert.Equal(["employees.manage", "orders.refund", "staff-roles.change"], sensitive);
    }

    [Theory]
    [Requirement("CC-DSH-002")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("orders.delete")]
    [InlineData("Orders.Search")] // case mismatch: exact ordinal only
    [InlineData("*")]
    public void Unknown_permission_names_do_not_resolve(string? name)
    {
        Assert.False(DashboardPermission.TryResolve(name, out var permission));
        Assert.Null(permission);
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    public void Permission_type_is_closed_no_public_construction_path()
    {
        Assert.Empty(typeof(DashboardPermission).GetConstructors());
    }
}
