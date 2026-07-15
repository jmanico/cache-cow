using System.Reflection;
using CacheCow.Modules.WholesaleB2B.Auth;
using CacheCow.Modules.WholesaleB2B.Partners;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.WholesaleB2B.Tests;

/// <summary>
/// Issue 049 (CC-WHS-002): onboarding is a dashboard-driven approval workflow —
/// exactly Draft -> Submitted -> (Approved | Rejected) and
/// Approved -> Suspended; everything else fails closed. Every transition is
/// audited append-before-effect through the append-only sink, an audit failure
/// denies the action, and no self-service activation path exists anywhere in
/// the module's public API surface (verified by reflection, CC-QA-005).
/// </summary>
public sealed class PartnerOnboardingWorkflowTests
{
    private static readonly PartnerOnboardingState[] AllStates = Enum.GetValues<PartnerOnboardingState>();

    /// <summary>Every workflow operation with its single legal source state and target.</summary>
    private static readonly (string Name, PartnerOnboardingState From, PartnerOnboardingState To, string Action)[] Operations =
    [
        (nameof(PartnerOnboardingWorkflow.Submit), PartnerOnboardingState.Draft, PartnerOnboardingState.Submitted, PartnerOnboardingWorkflow.SubmitAction),
        (nameof(PartnerOnboardingWorkflow.Approve), PartnerOnboardingState.Submitted, PartnerOnboardingState.Approved, PartnerOnboardingWorkflow.ApproveAction),
        (nameof(PartnerOnboardingWorkflow.Reject), PartnerOnboardingState.Submitted, PartnerOnboardingState.Rejected, PartnerOnboardingWorkflow.RejectAction),
        (nameof(PartnerOnboardingWorkflow.Suspend), PartnerOnboardingState.Approved, PartnerOnboardingState.Suspended, PartnerOnboardingWorkflow.SuspendAction),
    ];

    private static PartnerTenant Invoke(
        PartnerOnboardingWorkflow workflow, string operation, PartnerTenant tenant, DashboardActorProof actor) =>
        operation switch
        {
            nameof(PartnerOnboardingWorkflow.Submit) => workflow.Submit(tenant, actor),
            nameof(PartnerOnboardingWorkflow.Approve) => workflow.Approve(tenant, actor),
            nameof(PartnerOnboardingWorkflow.Reject) => workflow.Reject(tenant, actor),
            nameof(PartnerOnboardingWorkflow.Suspend) => workflow.Suspend(tenant, actor),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

    [Fact]
    [Requirement("CC-WHS-002")]
    public void A_new_tenant_is_always_a_non_active_draft()
    {
        var tenant = Fixtures.NewDraftTenant();

        // Issue 049, AC-01: persisted new records are non-active pending.
        Assert.Equal(PartnerOnboardingState.Draft, tenant.State);
        Assert.False(tenant.IsActive);
    }

    [Fact]
    [Requirement("CC-WHS-002")]
    public void Transition_matrix_is_exhaustively_enforced()
    {
        // Every (state x operation) pair: exactly the four ratified
        // transitions succeed; everything else — skips, reversals,
        // self-transitions, exits from terminal states, reinstatement and
        // resubmission (unratified, issue 049 Open Questions) — fails closed
        // with no state change and no audit event.
        foreach (var from in AllStates)
        {
            foreach (var (name, requiredFrom, to, _) in Operations)
            {
                var sink = new RecordingPartnerAuditSink();
                var workflow = new PartnerOnboardingWorkflow(sink);
                var tenant = Fixtures.TenantIn(from, workflow);
                Assert.Equal(from, tenant.State);
                var eventsBefore = sink.Events.Count;

                if (from == requiredFrom)
                {
                    var transitioned = Invoke(workflow, name, tenant, Fixtures.Staff);

                    Assert.Equal(to, transitioned.State);
                    Assert.Equal(eventsBefore + 1, sink.Events.Count);
                }
                else
                {
                    var exception = Assert.Throws<IllegalPartnerTransitionException>(
                        () => Invoke(workflow, name, tenant, Fixtures.Staff));

                    Assert.Equal(from, exception.From);
                    Assert.Equal(to, exception.To);
                    Assert.Equal(from, tenant.State);
                    Assert.Equal(eventsBefore, sink.Events.Count);
                }
            }
        }
    }

    [Fact]
    [Requirement("CC-WHS-002")]
    public void Every_transition_appends_a_complete_audit_event()
    {
        // Issue 049, AC-02; SECURITY.md, Logging rule 6: actor, action,
        // object, before/after, server-set timestamp.
        var sink = new RecordingPartnerAuditSink();
        var clock = new ManualTimeProvider(Fixtures.T0);
        var workflow = new PartnerOnboardingWorkflow(sink, clock);
        var tenant = Fixtures.NewDraftTenant();

        var submitted = workflow.Submit(tenant, Fixtures.Staff);
        clock.Advance(TimeSpan.FromMinutes(5));
        _ = workflow.Approve(submitted, Fixtures.Staff);

        Assert.Equal(2, sink.Events.Count);

        var approval = sink.Events[1];
        Assert.Equal("staff:approver-1", approval.Actor);
        Assert.Equal("admin", approval.ActorRole);
        Assert.Equal(PartnerOnboardingWorkflow.ApproveAction, approval.Action);
        Assert.Equal(tenant.Id, approval.PartnerId);
        Assert.Equal(PartnerOnboardingState.Submitted, approval.FromState);
        Assert.Equal(PartnerOnboardingState.Approved, approval.ToState);
        Assert.Equal(Fixtures.T0 + TimeSpan.FromMinutes(5), approval.Timestamp);
    }

    [Fact]
    [Requirement("CC-WHS-002")]
    public void An_audit_append_failure_denies_the_approval()
    {
        // Issue 049 AC-02 / SECURITY.md Logging rule 2: append-before-effect;
        // an unauditable approval never activates the tenant.
        var recordingWorkflow = new PartnerOnboardingWorkflow(new RecordingPartnerAuditSink());
        var submitted = Fixtures.TenantIn(PartnerOnboardingState.Submitted, recordingWorkflow);

        var failingWorkflow = new PartnerOnboardingWorkflow(new ThrowingPartnerAuditSink());

        Assert.Throws<InvalidOperationException>(() => failingWorkflow.Approve(submitted, Fixtures.Staff));
        Assert.Equal(PartnerOnboardingState.Submitted, submitted.State);
        Assert.False(submitted.IsActive);
    }

    [Fact]
    [Requirement("CC-WHS-002")]
    [Requirement("CC-QA-005")]
    public void Self_service_activation_is_unrepresentable_in_the_public_api_surface()
    {
        // Issue 049, AC-04: no self-service activation path exists. Verified
        // structurally: (1) PartnerTenant cannot be constructed or mutated
        // into Approved from outside, and (2) every public API in the module
        // assembly that yields a PartnerTenant either always yields Draft
        // (Create) or demands a DashboardActorProof (the workflow actions).
        var tenantType = typeof(PartnerTenant);

        Assert.Empty(tenantType.GetConstructors(BindingFlags.Public | BindingFlags.Instance));

        var stateSetter = tenantType.GetProperty(nameof(PartnerTenant.State))!.SetMethod;
        Assert.Null(stateSetter);

        var publicTenantReturningMembers = typeof(WholesaleB2BModule).Assembly
            .GetExportedTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Where(m => m.ReturnType == tenantType)
            .ToArray();

        Assert.NotEmpty(publicTenantReturningMembers);
        foreach (var member in publicTenantReturningMembers)
        {
            var isDraftOnlyFactory =
                member.DeclaringType == tenantType && member.Name == nameof(PartnerTenant.Create);
            var requiresDashboardActor =
                member.GetParameters().Any(p => p.ParameterType == typeof(DashboardActorProof));

            // Issue 054: the client directory is a host-adapted READ port
            // returning persisted tenancy state — it cannot construct or
            // transition a tenant (no public constructor, no state setter,
            // WithState is internal), so it opens no activation path.
            var isPersistedStateLookup =
                member.DeclaringType == typeof(IB2BClientDirectory)
                && member.Name == nameof(IB2BClientDirectory.FindByClientId);

            Assert.True(
                isDraftOnlyFactory || requiresDashboardActor || isPersistedStateLookup,
                $"{member.DeclaringType!.Name}.{member.Name} yields a PartnerTenant without a DashboardActorProof (issue 049, AC-04).");
        }

        // And the Draft-only factory really is draft-only.
        Assert.Equal(PartnerOnboardingState.Draft, Fixtures.NewDraftTenant().State);
    }

    [Fact]
    [Requirement("CC-WHS-002")]
    public void Actor_proof_requires_authenticated_identity_and_role()
    {
        Assert.Throws<WholesaleValidationException>(() => DashboardActorProof.ForAuthenticatedStaff("", "admin"));
        Assert.Throws<WholesaleValidationException>(() => DashboardActorProof.ForAuthenticatedStaff("staff:x", " "));
    }

    [Fact]
    [Requirement("CC-WHS-002")]
    public void Tenant_creation_rejects_invalid_identity_capture()
    {
        var id = PartnerId.Parse("partner-x");

        // No identity at all.
        Assert.Throws<WholesaleValidationException>(
            () => PartnerTenant.Create(id, "Valid Name", []));

        // Two identities for the same market.
        Assert.Throws<WholesaleValidationException>(
            () => PartnerTenant.Create(id, "Valid Name", [Fixtures.DeIdentity(), Fixtures.DeIdentity()]));

        // Missing legal name and uninitialized partner id.
        Assert.Throws<WholesaleValidationException>(
            () => PartnerTenant.Create(id, " ", [Fixtures.DeIdentity()]));
        Assert.Throws<WholesaleValidationException>(
            () => PartnerTenant.Create(default, "Valid Name", [Fixtures.DeIdentity()]));
    }
}
