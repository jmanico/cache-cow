using CacheCow.Modules.BackOffice.Auditing;
using CacheCow.Modules.BackOffice.Rbac;
using CacheCow.SharedKernel.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CacheCow.Modules.BackOffice.Tests;

/// <summary>
/// Issues 080/081: the module registers resolvable services whose provisional
/// defaults fail closed — no role–permission matrix and no step-up policy, so
/// every permission check denies until humans author them (issue 080, Open
/// Questions) — and the in-memory append-only store sits behind both the full
/// store port and the write-only sink other contexts adapt onto (issue 081).
/// </summary>
public sealed class ModuleRegistrationTests
{
    private static ServiceProvider BuildProvider() =>
        new ServiceCollection().AddBackOfficeModule().BuildServiceProvider();

    [Fact]
    [Requirement("CC-DSH-002")]
    [Requirement("CC-DSH-004")]
    public void Module_registers_authorization_and_audit_services()
    {
        using var provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService<IDashboardAuthorizationService>());
        Assert.NotNull(provider.GetRequiredService<IAuditStore>());
        Assert.NotNull(provider.GetRequiredService<IAuditEventSink>());
        Assert.NotNull(provider.GetRequiredService<IWormReplicationSink>());
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    [Requirement("CC-QA-005")]
    public void Default_wiring_denies_every_permission_check_fail_closed()
    {
        using var provider = BuildProvider();
        var service = provider.GetRequiredService<IDashboardAuthorizationService>();

        foreach (var role in StaffRole.All)
        {
            foreach (var permission in DashboardPermission.All)
            {
                var decision = service.CheckPermission(
                    StaffContext.ForAuthenticatedStaff("staff-1", role.Name, DateTimeOffset.UtcNow),
                    permission);

                Assert.False(decision.IsGranted);
                Assert.Equal(AccessDenialReason.MatrixNotConfigured, decision.Denial);
            }
        }
    }

    [Fact]
    [Requirement("CC-DSH-004")]
    [Requirement("CC-SEC-020")]
    public void Store_and_sink_are_the_same_append_only_instance()
    {
        using var provider = BuildProvider();

        var store = provider.GetRequiredService<IAuditStore>();
        var sink = provider.GetRequiredService<IAuditEventSink>();
        Assert.Same(store, sink);

        var auditEvent = BackOfficeTestData.Event();
        sink.Append(auditEvent);

        Assert.Equal(auditEvent, Assert.Single(store.Query(new AuditQuery())));
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    public void Host_supplied_matrix_and_step_up_policy_take_precedence_over_the_fail_closed_defaults()
    {
        // TryAdd semantics: once a human-authored matrix and a ratified
        // step-up number exist, the host registers them first and the
        // module's unconfigured defaults yield.
        var matrix = RolePermissionMatrix.Create(new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
        {
            ["ops-agent"] = ["orders.search"],
        });
        var services = new ServiceCollection();
        services.AddSingleton<IRolePermissionMatrixProvider>(new ConfiguredRolePermissionMatrixProvider(matrix));
        services.AddSingleton<IStepUpPolicyProvider>(new ConfiguredStepUpPolicyProvider(StepUpPolicy.Create(TimeSpan.FromMinutes(10))));
        using var provider = services.AddBackOfficeModule().BuildServiceProvider();

        var service = provider.GetRequiredService<IDashboardAuthorizationService>();
        var decision = service.CheckPermission(
            StaffContext.ForAuthenticatedStaff("staff-1", "ops-agent"),
            DashboardPermission.SearchOrders);

        Assert.True(decision.IsGranted);
    }
}
