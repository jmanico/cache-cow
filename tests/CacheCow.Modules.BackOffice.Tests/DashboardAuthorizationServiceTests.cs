using CacheCow.Modules.BackOffice.Rbac;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.BackOffice.Tests;

/// <summary>
/// Issue 080: matrix-driven enforcement fails closed on every abnormal
/// condition — missing matrix, unknown role, missing step-up policy, provider
/// fault — and grants nothing implicitly (CC-DSH-002; CC-QA-005; SECURITY.md,
/// Authentication rules 2 and 8; Logging rule 2). The grants used here are a
/// TEST matrix only: the production matrix content needs human authoring
/// (issue 080, Open Questions).
/// </summary>
public sealed class DashboardAuthorizationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The test matrix: one deliberately narrow grant set per role. hr-admin
    /// is the only role holding employees.manage (CC-DSH-005 pointer, issue
    /// 087); admin holds role changes and partner capabilities only —
    /// explicitly NOT everything.
    /// </summary>
    private static RolePermissionMatrix TestMatrix() =>
        RolePermissionMatrix.Create(new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
        {
            ["sales-viewer"] = ["analytics.view"],
            ["ops-agent"] = ["orders.search", "orders.transition", "fulfillment.cross-region-override"],
            ["finance"] = ["orders.refund", "invoices.manage"],
            ["hr-admin"] = ["employees.manage"],
            ["admin"] = ["staff-roles.change", "partners.manage", "partners.approve"],
        });

    private static DashboardAuthorizationService Service(
        IRolePermissionMatrixProvider? matrixProvider = null,
        IStepUpPolicyProvider? stepUpProvider = null,
        TimeProvider? clock = null) =>
        new(
            matrixProvider ?? new ConfiguredRolePermissionMatrixProvider(TestMatrix()),
            stepUpProvider ?? new ConfiguredStepUpPolicyProvider(StepUpPolicy.Create(TimeSpan.FromMinutes(10))),
            clock ?? new FixedTimeProvider(Now));

    private static StaffContext Staff(string role, DateTimeOffset? reauthAt = null) =>
        StaffContext.ForAuthenticatedStaff("staff-1", role, reauthAt);

    [Fact]
    [Requirement("CC-DSH-002")]
    [Requirement("CC-QA-005")]
    public void Without_a_configured_matrix_every_check_denies_for_every_role_and_permission()
    {
        var service = Service(matrixProvider: new UnconfiguredRolePermissionMatrixProvider());

        foreach (var role in StaffRole.All)
        {
            foreach (var permission in DashboardPermission.All)
            {
                var decision = service.CheckPermission(Staff(role.Name, reauthAt: Now), permission);

                Assert.False(decision.IsGranted);
                Assert.Equal(AccessDenialReason.MatrixNotConfigured, decision.Denial);
            }
        }
    }

    [Theory]
    [Requirement("CC-DSH-002")]
    [InlineData("superuser")]
    [InlineData("")]
    [InlineData("Admin")]
    public void Unknown_role_claims_are_denied_even_with_a_matrix_configured(string roleName)
    {
        var decision = Service().CheckPermission(Staff(roleName), DashboardPermission.SearchOrders);

        Assert.False(decision.IsGranted);
        Assert.Equal(AccessDenialReason.UnknownRole, decision.Denial);
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    [Requirement("CC-QA-005")]
    public void Every_role_is_denied_every_permission_the_test_matrix_does_not_grant_it()
    {
        var service = Service();
        var matrix = TestMatrix();

        foreach (var role in StaffRole.All)
        {
            var granted = matrix.GrantsFor(role);
            foreach (var permission in DashboardPermission.All.Where(p => !granted.Contains(p)))
            {
                var decision = service.CheckPermission(Staff(role.Name, reauthAt: Now), permission);

                Assert.False(decision.IsGranted);
                Assert.Equal(AccessDenialReason.PermissionNotGranted, decision.Denial);
            }
        }
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    [Requirement("CC-QA-005")]
    public void Every_granted_non_sensitive_permission_is_allowed_without_reauth()
    {
        var service = Service();
        var matrix = TestMatrix();

        foreach (var role in StaffRole.All)
        {
            foreach (var permission in matrix.GrantsFor(role).Where(p => !p.RequiresRecentReauth))
            {
                var decision = service.CheckPermission(Staff(role.Name), permission);

                Assert.True(decision.IsGranted);
                Assert.Equal(AccessDenialReason.None, decision.Denial);
            }
        }
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    [Requirement("CC-QA-005")]
    public void Only_hr_admin_reaches_employee_management()
    {
        // CC-DSH-005 pointer: employee records are restricted to hr-admin;
        // issue 087 builds its field-level protections on this matrix entry.
        var service = Service();

        var hrDecision = service.CheckPermission(Staff("hr-admin", reauthAt: Now), DashboardPermission.ManageEmployees);
        Assert.True(hrDecision.IsGranted);

        foreach (var role in StaffRole.All.Where(r => r != StaffRole.HrAdmin))
        {
            var decision = service.CheckPermission(Staff(role.Name, reauthAt: Now), DashboardPermission.ManageEmployees);

            Assert.False(decision.IsGranted);
            Assert.Equal(AccessDenialReason.PermissionNotGranted, decision.Denial);
        }
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    [Requirement("CC-QA-005")]
    public void Admin_is_not_implicitly_everything()
    {
        // The admin role holds exactly its matrix grants; refunds, employee
        // records, order handling, and analytics all deny (no wildcard logic).
        var service = Service();
        var admin = Staff("admin", reauthAt: Now);

        Assert.False(service.CheckPermission(admin, DashboardPermission.IssueRefunds).IsGranted);
        Assert.False(service.CheckPermission(admin, DashboardPermission.ManageEmployees).IsGranted);
        Assert.False(service.CheckPermission(admin, DashboardPermission.SearchOrders).IsGranted);
        Assert.False(service.CheckPermission(admin, DashboardPermission.ViewSalesAnalytics).IsGranted);

        Assert.True(service.CheckPermission(admin, DashboardPermission.ChangeStaffRoles).IsGranted);
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    public void Fresh_reauthentication_allows_sensitive_permissions()
    {
        var decision = Service().CheckPermission(
            Staff("finance", reauthAt: Now.AddMinutes(-5)),
            DashboardPermission.IssueRefunds);

        Assert.True(decision.IsGranted);
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    public void Stale_reauthentication_denies_sensitive_permissions()
    {
        var decision = Service().CheckPermission(
            Staff("finance", reauthAt: Now.AddMinutes(-11)),
            DashboardPermission.IssueRefunds);

        Assert.False(decision.IsGranted);
        Assert.Equal(AccessDenialReason.ReauthenticationStale, decision.Denial);
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    public void Missing_reauthentication_denies_sensitive_permissions()
    {
        var decision = Service().CheckPermission(
            Staff("finance", reauthAt: null),
            DashboardPermission.IssueRefunds);

        Assert.False(decision.IsGranted);
        Assert.Equal(AccessDenialReason.ReauthenticationMissing, decision.Denial);
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    public void Future_dated_reauthentication_is_denied_not_treated_as_fresh()
    {
        var decision = Service().CheckPermission(
            Staff("finance", reauthAt: Now.AddMinutes(5)),
            DashboardPermission.IssueRefunds);

        Assert.False(decision.IsGranted);
        Assert.Equal(AccessDenialReason.ReauthenticationStale, decision.Denial);
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    public void Without_a_step_up_policy_sensitive_permissions_deny_and_non_sensitive_are_unaffected()
    {
        // The step-up max age is required, unratified configuration (issue
        // 080; SECURITY.md, Authentication rule 2 ratifies only the 12-hour
        // session lifetime): absent a human-supplied number, fail closed.
        var service = Service(stepUpProvider: new UnconfiguredStepUpPolicyProvider());

        var sensitive = service.CheckPermission(Staff("finance", reauthAt: Now), DashboardPermission.IssueRefunds);
        Assert.False(sensitive.IsGranted);
        Assert.Equal(AccessDenialReason.StepUpPolicyNotConfigured, sensitive.Denial);

        var nonSensitive = service.CheckPermission(Staff("finance"), DashboardPermission.ManageInvoices);
        Assert.True(nonSensitive.IsGranted);
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    public void Stale_reauthentication_does_not_affect_non_sensitive_permissions()
    {
        var decision = Service().CheckPermission(
            Staff("ops-agent", reauthAt: Now.AddHours(-11)),
            DashboardPermission.SearchOrders);

        Assert.True(decision.IsGranted);
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    [Requirement("CC-QA-005")]
    public void A_faulting_authorization_path_is_a_denial_never_a_bypass_or_escape()
    {
        // SECURITY.md, Logging rule 2: any exception in an authorization path
        // is a denial. The throwing provider must surface as a denial, not
        // as an exception the endpoint could mishandle open.
        var service = Service(matrixProvider: new ThrowingMatrixProvider());

        var decision = service.CheckPermission(Staff("admin", reauthAt: Now), DashboardPermission.ChangeStaffRoles);

        Assert.False(decision.IsGranted);
        Assert.Equal(AccessDenialReason.AuthorizationFault, decision.Denial);
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    public void Null_arguments_fail_closed_as_denials()
    {
        var service = Service();

        var decision = service.CheckPermission(null!, DashboardPermission.SearchOrders);

        Assert.False(decision.IsGranted);
        Assert.Equal(AccessDenialReason.AuthorizationFault, decision.Denial);
    }

    [Theory]
    [Requirement("CC-DSH-002")]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(13 * 60)] // beyond the ratified 12-hour session lifetime
    public void Out_of_bounds_step_up_max_age_is_rejected_at_load(int minutes)
    {
        Assert.Throws<RbacConfigurationException>(() => StepUpPolicy.Create(TimeSpan.FromMinutes(minutes)));
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    public void Step_up_max_age_within_the_ratified_session_lifetime_loads()
    {
        var policy = StepUpPolicy.Create(TimeSpan.FromMinutes(15));

        Assert.Equal(TimeSpan.FromMinutes(15), policy.MaxReauthAge);
        Assert.Equal(TimeSpan.FromHours(12), StepUpPolicy.RatifiedMaxStaffSessionLifetime);
    }
}
