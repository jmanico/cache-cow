using CacheCow.Modules.BackOffice.Dashboard;
using CacheCow.Modules.BackOffice.Partners;
using CacheCow.Modules.BackOffice.Rbac;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.BackOffice.Tests;

/// <summary>
/// Issue 085: partner-management gating, the no-self-service approval gate,
/// and audit-before-effect (CC-DSH-003, CC-WHS-002, CC-DSH-004, CC-QA-005).
/// Grants come from the TEST matrix — which role may approve partners is
/// unresolved (issue 085, Open Questions).
/// </summary>
public sealed class DashboardPartnerServiceTests
{
    private readonly RecordingAuditSink audit = new();
    private readonly FakePartnerDirectory directory = new();
    private readonly FakePartnerWorkflow workflow = new();

    public DashboardPartnerServiceTests() => directory.Add(DashboardTestData.Partner());

    private DashboardPartnerService Service(IStepUpPolicyProvider? stepUpProvider = null)
    {
        var clock = new FixedTimeProvider(DashboardTestData.Now);
        return new DashboardPartnerService(
            new DashboardAuthorizationService(
                new ConfiguredRolePermissionMatrixProvider(DashboardTestMatrix.Create()),
                stepUpProvider ?? new ConfiguredStepUpPolicyProvider(StepUpPolicy.Create(TimeSpan.FromMinutes(5))),
                clock),
            directory,
            workflow,
            audit,
            clock);
    }

    // ---- list and detail (AC-01, AC-06) -----------------------------------

    [Fact]
    [Requirement("CC-DSH-003")]
    [Requirement("CC-WHS-002")]
    public void Search_WithGrantedRole_ReturnsPartners()
    {
        var result = Service().Search(
            DashboardTestData.Staff("admin"), DashboardPartnerQuery.Create(), "corr-1");

        Assert.Equal(DashboardActionStatus.Completed, result.Status);
        Assert.Single(result.Value!.Items);
    }

    [Fact]
    [Requirement("CC-DSH-003")]
    [Requirement("CC-WHS-002")]
    public void Find_WithGrantedRole_ReturnsPerMarketBusinessIdentity()
    {
        // AC-01: the detail view shows the per-market business identity the
        // onboarding workflow captured (USt-IdNr. for DE).
        var result = Service().Find(DashboardTestData.Staff("admin"), DashboardTestData.PartnerId, "corr-1");

        Assert.Equal(DashboardActionStatus.Completed, result.Status);
        var identity = Assert.Single(result.Value!.Identities);
        Assert.Equal("USt-IdNr.", identity.Kind);
        Assert.Equal(60, result.Value.PaymentTermsNetDays);
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    [Requirement("CC-QA-005")]
    public void Find_WithRoleLackingGrant_DeniesBeforeAnyLookup()
    {
        // AC-06: a role without partner management gets nothing (404 at HTTP).
        var result = Service().Find(DashboardTestData.Staff("ops-agent"), DashboardTestData.PartnerId, "corr-1");

        Assert.Equal(DashboardActionStatus.Denied, result.Status);
        Assert.Equal(AccessDenialReason.PermissionNotGranted, result.DenialReason);
        Assert.Equal(0, directory.FindCalls);
    }

    [Fact]
    [Requirement("CC-WHS-002")]
    public void Find_UnknownPartner_IsNotFound()
    {
        var result = Service().Find(DashboardTestData.Staff("admin"), "partner-nope", "corr-1");
        Assert.Equal(DashboardActionStatus.NotFound, result.Status);
    }

    // ---- the approval gate (AC-02, AC-03, AC-05) --------------------------

    [Fact]
    [Requirement("CC-WHS-002")]
    [Requirement("CC-DSH-004")]
    public void Approve_WithGrantedRole_AuditsBeforeDrivingTheWorkflow()
    {
        var result = Service().Approve(DashboardTestData.Staff("admin"), DashboardTestData.PartnerId, "corr-1");

        Assert.Equal(DashboardActionStatus.Completed, result.Status);
        Assert.Equal(DashboardPartnerState.Approved, result.Value!.State);

        // AC-02: approval is what activates the partner, and it went through
        // the owning context's workflow.
        var invocation = Assert.Single(workflow.Invocations);
        Assert.Equal("approve", invocation.Action);

        // AC-05: actor, action, object, before/after, timestamp (CC-DSH-004).
        var attempt = audit.Appended[0];
        Assert.Equal("staff-1", attempt.Actor);
        Assert.Equal("admin", attempt.ActorRole);
        Assert.Equal("partners.approve", attempt.Action);
        Assert.Equal("partner", attempt.ObjectType);
        Assert.Equal(DashboardTestData.PartnerId, attempt.ObjectId);
        Assert.Equal("state=Submitted", attempt.BeforeSummary);
        Assert.Equal("requested state=Approved", attempt.AfterSummary);
        Assert.Equal("partners.approve.applied", audit.Appended[1].Action);
    }

    [Fact]
    [Requirement("CC-WHS-002")]
    [Requirement("CC-DSH-004")]
    public void Approve_WhenAuditAppendFails_DeniesAndNeverActivatesThePartner()
    {
        audit.Throw = true;

        var result = Service().Approve(DashboardTestData.Staff("admin"), DashboardTestData.PartnerId, "corr-1");

        Assert.Equal(DashboardActionStatus.Unavailable, result.Status);

        // "If the audit write fails, the action fails — no unaudited
        // privileged action completes" (issue 085, Failure Behavior).
        Assert.Empty(workflow.Invocations);
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    [Requirement("CC-QA-005")]
    public void Approve_WithRoleLackingApprovalGrant_IsRefused()
    {
        // ops-agent manages orders, not partners: it cannot activate anyone.
        var result = Service().Approve(DashboardTestData.Staff("ops-agent"), DashboardTestData.PartnerId, "corr-1");

        Assert.Equal(DashboardActionStatus.Denied, result.Status);
        Assert.Equal(AccessDenialReason.PermissionNotGranted, result.DenialReason);
        Assert.Empty(workflow.Invocations);
        Assert.Empty(audit.Appended);
    }

    [Fact]
    [Requirement("CC-WHS-002")]
    public void Approve_WhenTheWorkflowRefuses_IsAConflictWithNoActivationClaimed()
    {
        // E.g. approving a record that was never submitted. The owning context
        // rules on legality; this module relays it.
        workflow.Outcome = DashboardPartnerCommandOutcome.Rejected;

        var result = Service().Approve(DashboardTestData.Staff("admin"), DashboardTestData.PartnerId, "corr-1");

        Assert.Equal(DashboardActionStatus.Conflict, result.Status);
        Assert.Equal("partners.approve.rejected", audit.Appended[1].Action);
    }

    [Fact]
    [Requirement("CC-WHS-002")]
    public void Approve_WhenTheWorkflowFaults_FailsClosed()
    {
        workflow.Throw = true;

        var result = Service().Approve(DashboardTestData.Staff("admin"), DashboardTestData.PartnerId, "corr-1");

        // A partner is never activated by a failed or ambiguous workflow step
        // (issue 085, Failure Behavior).
        Assert.Equal(DashboardActionStatus.Unavailable, result.Status);
        Assert.Equal("partners.approve.unavailable", audit.Appended[1].Action);
    }

    [Fact]
    [Requirement("CC-WHS-002")]
    public void Approve_CarriesTheAuthorizedActorToTheOwningContext()
    {
        // The workflow demands actor evidence; the Back Office is the only
        // minter, and only after authorization (CC-WHS-002: no self-service).
        Service().Approve(DashboardTestData.Staff("admin", "staff-77"), DashboardTestData.PartnerId, "corr-1");

        var actor = Assert.Single(workflow.Invocations).Actor;
        Assert.Equal("staff-77", actor.ActorId);
        Assert.Equal("admin", actor.Role);
    }

    [Fact]
    [Requirement("CC-WHS-002")]
    public void Reject_LeavesThePartnerInactive()
    {
        var result = Service().Reject(DashboardTestData.Staff("admin"), DashboardTestData.PartnerId, "corr-1");

        Assert.Equal(DashboardActionStatus.Completed, result.Status);
        Assert.Equal(DashboardPartnerState.Rejected, result.Value!.State);
        Assert.Equal("reject", Assert.Single(workflow.Invocations).Action);
    }

    [Fact]
    [Requirement("CC-WHS-002")]
    public void Suspend_RequiresTheApprovalPermission()
    {
        var result = Service().Suspend(DashboardTestData.Staff("admin"), DashboardTestData.PartnerId, "corr-1");
        Assert.Equal(DashboardActionStatus.Completed, result.Status);

        var refused = Service().Suspend(DashboardTestData.Staff("ops-agent"), DashboardTestData.PartnerId, "corr-1");
        Assert.Equal(DashboardActionStatus.Denied, refused.Status);
    }

    /// <summary>
    /// Issue 085, Open Questions: whether partner approval requires step-up
    /// re-authentication is NOT stated, and this module asserts nothing either
    /// way — it defers entirely to the permission's RequiresRecentReauth flag.
    /// This test pins that deference: partner permissions are not marked
    /// sensitive today, so approval works without a step-up. If a human
    /// decides approval IS sensitive, flipping the flag on the permission
    /// changes this behavior with no service change — and this test is the
    /// tripwire that surfaces the decision.
    /// </summary>
    [Fact]
    [Requirement("CC-WHS-002")]
    [Requirement("CC-DSH-001")]
    public void Approve_TodayNeedsNoStepUp_BecauseThePermissionIsNotMarkedSensitive()
    {
        Assert.False(DashboardPermission.ApprovePartners.RequiresRecentReauth);

        var result = Service().Approve(
            DashboardTestData.StaffWithoutStepUp("admin"), DashboardTestData.PartnerId, "corr-1");

        Assert.Equal(DashboardActionStatus.Completed, result.Status);
    }
}
