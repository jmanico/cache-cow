using CacheCow.Modules.BackOffice.Dashboard;
using CacheCow.Modules.BackOffice.Orders;
using CacheCow.Modules.BackOffice.Rbac;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.BackOffice.Tests;

/// <summary>
/// Issue 082: order management gating, step-up, audit-before-effect, and
/// fail-closed behavior (CC-DSH-003, CC-ORD-006, CC-DSH-004, CC-QA-005).
///
/// The grants come from <see cref="DashboardTestMatrix"/>, a TEST matrix only —
/// the production matrix content needs human authoring (issue 080/082, Open
/// Questions). These tests assert enforcement, never policy.
/// </summary>
public sealed class DashboardOrderServiceTests
{
    private readonly RecordingAuditSink audit = new();
    private readonly FakeOrderReader reader = new();
    private readonly FakeOrderCommands commands = new();

    public DashboardOrderServiceTests() => reader.Add(DashboardTestData.Order());

    private DashboardOrderService Service(
        IRolePermissionMatrixProvider? matrixProvider = null,
        IStepUpPolicyProvider? stepUpProvider = null)
    {
        var clock = new FixedTimeProvider(DashboardTestData.Now);
        var authorization = new DashboardAuthorizationService(
            matrixProvider ?? new ConfiguredRolePermissionMatrixProvider(DashboardTestMatrix.Create()),
            stepUpProvider ?? new ConfiguredStepUpPolicyProvider(StepUpPolicy.Create(TimeSpan.FromMinutes(5))),
            clock);

        return new DashboardOrderService(authorization, reader, commands, audit, clock);
    }

    // ---- search: permission enforcement (AC-01, AC-06) --------------------

    [Fact]
    [Requirement("CC-DSH-003")]
    [Requirement("CC-DSH-002")]
    public void Search_WithGrantedRole_ReturnsRows()
    {
        var result = Service().Search(
            DashboardTestData.Staff("ops-agent"), DashboardOrderSearchQuery.Create(), "corr-1");

        Assert.Equal(DashboardActionStatus.Completed, result.Status);
        Assert.Single(result.Value!.Items);
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    [Requirement("CC-QA-005")]
    public void Search_WithRoleLackingGrant_DeniesAndNeverReadsOrders()
    {
        // sales-viewer holds analytics.view only.
        var result = Service().Search(
            DashboardTestData.Staff("sales-viewer"), DashboardOrderSearchQuery.Create(), "corr-1");

        Assert.Equal(DashboardActionStatus.Denied, result.Status);
        Assert.Equal(AccessDenialReason.PermissionNotGranted, result.DenialReason);

        // The denial precedes the read: no order data is touched for an
        // unauthorized caller.
        Assert.Equal(0, reader.SearchCalls);
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    public void Search_WithNoMatrixConfigured_Denies()
    {
        // The unconfigured provider is the module's shipped default: with no
        // human-authored matrix, everything denies (issue 080).
        var result = Service(new UnconfiguredRolePermissionMatrixProvider()).Search(
            DashboardTestData.Staff("ops-agent"), DashboardOrderSearchQuery.Create(), "corr-1");

        Assert.Equal(DashboardActionStatus.Denied, result.Status);
        Assert.Equal(AccessDenialReason.MatrixNotConfigured, result.DenialReason);
        Assert.Equal(0, reader.SearchCalls);
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    public void Search_WithUnknownRole_Denies()
    {
        var result = Service().Search(
            DashboardTestData.Staff("not-a-role"), DashboardOrderSearchQuery.Create(), "corr-1");

        Assert.Equal(DashboardActionStatus.Denied, result.Status);
        Assert.Equal(AccessDenialReason.UnknownRole, result.DenialReason);
    }

    [Fact]
    [Requirement("CC-DSH-003")]
    public void Search_WhenReaderFaults_FailsClosedRatherThanShowingAnEmptyGrid()
    {
        reader.Throw = true;

        var result = Service().Search(
            DashboardTestData.Staff("ops-agent"), DashboardOrderSearchQuery.Create(), "corr-1");

        // Unavailable, NOT Completed-with-zero-rows: an empty grid would be a
        // fabricated operational picture (SECURITY.md, Logging rule 2).
        Assert.Equal(DashboardActionStatus.Unavailable, result.Status);
    }

    // ---- transitions (AC-02, AC-03) ---------------------------------------

    [Fact]
    [Requirement("CC-ORD-006")]
    [Requirement("CC-DSH-004")]
    public void Transition_WhenGranted_AuditsBeforeInvokingTheStateMachine()
    {
        var result = Service().Transition(
            DashboardTestData.Staff("ops-agent"), DashboardTestData.OrderRef,
            DashboardOrderState.Delivered, "corr-1");

        Assert.Equal(DashboardActionStatus.Completed, result.Status);
        Assert.Equal(DashboardOrderState.Delivered, result.Value!.State);
        Assert.Single(commands.Transitions);

        // The attempt event carries actor, role, action, object, before/after
        // and correlation (CC-DSH-004; SECURITY.md, Logging rule 6), and its
        // after-summary is framed as a REQUEST, not an accomplished state.
        var attempt = audit.Appended[0];
        Assert.Equal("staff-1", attempt.Actor);
        Assert.Equal("ops-agent", attempt.ActorRole);
        Assert.Equal("orders.transition", attempt.Action);
        Assert.Equal("order", attempt.ObjectType);
        Assert.Equal(DashboardTestData.OrderRef, attempt.ObjectId);
        Assert.Equal("state=shipped", attempt.BeforeSummary);
        Assert.Equal("requested state=delivered", attempt.AfterSummary);
        Assert.Equal("corr-1", attempt.CorrelationId);

        // ...followed by the outcome record (the compensating-event mechanism,
        // SECURITY.md, Logging rule 6).
        Assert.Equal("orders.transition.applied", audit.Appended[1].Action);
        Assert.Equal("state=delivered", audit.Appended[1].AfterSummary);
    }

    [Fact]
    [Requirement("CC-DSH-004")]
    [Requirement("CC-ORD-006")]
    public void Transition_WhenAuditAppendFails_DeniesAndNeverInvokesTheStateMachine()
    {
        audit.Throw = true;

        var result = Service().Transition(
            DashboardTestData.Staff("ops-agent"), DashboardTestData.OrderRef,
            DashboardOrderState.Delivered, "corr-1");

        Assert.Equal(DashboardActionStatus.Unavailable, result.Status);

        // THE point of append-before-effect: no unaudited state change is
        // reachable, so the port must never have run (issue 081, Failure
        // Behavior; issue 082, Failure Behavior).
        Assert.Empty(commands.Transitions);
        Assert.Equal(0, commands.Invocations);
    }

    [Fact]
    [Requirement("CC-ORD-006")]
    public void Transition_WhenStateMachineRejects_IsAConflictWithNoStateChangeClaimed()
    {
        // AC-03: an illegal transition — e.g. delivered -> packed, or out of a
        // terminal branch — is refused by the OWNING context, and this module
        // relays that faithfully. It never predicts the ruling itself.
        commands.TransitionOutcome = DashboardOrderCommandOutcome.Rejected;

        var result = Service().Transition(
            DashboardTestData.Staff("ops-agent"), DashboardTestData.OrderRef,
            DashboardOrderState.Packed, "corr-1");

        Assert.Equal(DashboardActionStatus.Conflict, result.Status);

        // The audit trail does not lie about it: the attempt is recorded, and
        // a compensating event records that nothing changed.
        Assert.Equal("orders.transition", audit.Appended[0].Action);
        Assert.Equal("orders.transition.rejected", audit.Appended[1].Action);
        Assert.Contains("no change", audit.Appended[1].AfterSummary, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-ORD-006")]
    public void Transition_WhenPortFaults_FailsClosedAndRecordsTheOutcomeAsUnknown()
    {
        commands.Throw = true;

        var result = Service().Transition(
            DashboardTestData.Staff("ops-agent"), DashboardTestData.OrderRef,
            DashboardOrderState.Delivered, "corr-1");

        Assert.Equal(DashboardActionStatus.Unavailable, result.Status);

        // A fault means the outcome is UNKNOWN — the context may have applied
        // the change before failing to answer. Claiming "no change" would be a
        // fabrication.
        Assert.Equal("orders.transition.unavailable", audit.Appended[1].Action);
        Assert.Contains("unknown", audit.Appended[1].AfterSummary, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    [Requirement("CC-QA-005")]
    public void Transition_WithRoleLackingGrant_DeniesBeforeReadingOrAuditing()
    {
        // finance holds orders.refund but NOT orders.transition.
        var result = Service().Transition(
            DashboardTestData.Staff("finance"), DashboardTestData.OrderRef,
            DashboardOrderState.Delivered, "corr-1");

        Assert.Equal(DashboardActionStatus.Denied, result.Status);
        Assert.Equal(AccessDenialReason.PermissionNotGranted, result.DenialReason);
        Assert.Equal(0, reader.FindCalls);
        Assert.Equal(0, commands.Invocations);
        Assert.Empty(audit.Appended);
    }

    [Fact]
    [Requirement("CC-ORD-006")]
    public void Transition_ToUnknownOrder_IsNotFoundAndNeverAudits()
    {
        var result = Service().Transition(
            DashboardTestData.Staff("ops-agent"), "CC-ORD-NOPE", DashboardOrderState.Delivered, "corr-1");

        // Indistinguishable from the denial response at the HTTP boundary
        // (SECURITY.md, Authentication rule 9).
        Assert.Equal(DashboardActionStatus.NotFound, result.Status);
        Assert.Empty(audit.Appended);
        Assert.Equal(0, commands.Invocations);
    }

    // ---- refunds (AC-04, AC-05) -------------------------------------------

    [Fact]
    [Requirement("CC-DSH-001")]
    [Requirement("CC-ORD-006")]
    [Requirement("CC-PRC-003")]
    public void Refund_WithFreshStepUp_AppliesAndAuditsTheCanonicalAmountAsFinancial()
    {
        var result = Service().Refund(DashboardTestData.Staff("finance"), DashboardTestData.OrderRef, "corr-1");

        Assert.Equal(DashboardActionStatus.Completed, result.Status);
        Assert.Equal(DashboardOrderState.Refunded, result.Value!.State);

        // The amount is the owning context's canonical figure, in integer
        // minor units (CC-PRC-003/CC-PRC-005) — never a client's.
        Assert.Equal(29_988, result.Value.RefundedAmount.MinorUnits);
        Assert.Equal(Currency.Eur, result.Value.RefundedAmount.Currency);

        // Money movement is a financial action: 7-year retention (CC-DSH-004,
        // ratified 2026-07-15).
        Assert.Equal(Auditing.AuditRetentionClass.Financial, audit.Appended[0].RetentionClass);
        Assert.Equal("orders.refund", audit.Appended[0].Action);
        Assert.Contains("total=29988 EUR", audit.Appended[0].BeforeSummary, StringComparison.Ordinal);
        Assert.Equal("orders.refund.applied", audit.Appended[1].Action);
        Assert.Contains("refunded=29988 EUR", audit.Appended[1].AfterSummary, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-DSH-001")]
    [Requirement("CC-QA-005")]
    public void Refund_WithoutAnyStepUp_IsRefusedAndNeverMovesMoney()
    {
        // AC-04: a refund on session trust alone must not execute.
        var result = Service().Refund(
            DashboardTestData.StaffWithoutStepUp("finance"), DashboardTestData.OrderRef, "corr-1");

        Assert.Equal(DashboardActionStatus.Denied, result.Status);
        Assert.Equal(AccessDenialReason.ReauthenticationMissing, result.DenialReason);
        Assert.Empty(commands.Refunds);
        Assert.Empty(audit.Appended);
    }

    [Fact]
    [Requirement("CC-DSH-001")]
    [Requirement("CC-QA-005")]
    public void Refund_WithStaleStepUp_IsRefusedAndNeverMovesMoney()
    {
        // Re-authentication six hours ago against a five-minute max age: the
        // session is still valid (12-hour lifetime) but the step-up is stale.
        var result = Service().Refund(
            DashboardTestData.StaffWithStaleStepUp("finance"), DashboardTestData.OrderRef, "corr-1");

        Assert.Equal(DashboardActionStatus.Denied, result.Status);
        Assert.Equal(AccessDenialReason.ReauthenticationStale, result.DenialReason);
        Assert.Empty(commands.Refunds);
        Assert.Empty(audit.Appended);
    }

    [Fact]
    [Requirement("CC-DSH-001")]
    public void Refund_WithNoStepUpPolicyConfigured_IsRefused()
    {
        // The step-up max age is unratified configuration; while it is absent,
        // sensitive actions deny (the module's shipped default).
        var result = Service(stepUpProvider: new UnconfiguredStepUpPolicyProvider())
            .Refund(DashboardTestData.Staff("finance"), DashboardTestData.OrderRef, "corr-1");

        Assert.Equal(DashboardActionStatus.Denied, result.Status);
        Assert.Equal(AccessDenialReason.StepUpPolicyNotConfigured, result.DenialReason);
        Assert.Empty(commands.Refunds);
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    [Requirement("CC-QA-005")]
    public void Refund_WithRoleLackingGrant_IsRefusedEvenWithFreshStepUp()
    {
        // ops-agent has a fresh step-up but no orders.refund grant: recency
        // never substitutes for a grant.
        var result = Service().Refund(DashboardTestData.Staff("ops-agent"), DashboardTestData.OrderRef, "corr-1");

        Assert.Equal(DashboardActionStatus.Denied, result.Status);
        Assert.Equal(AccessDenialReason.PermissionNotGranted, result.DenialReason);
        Assert.Empty(commands.Refunds);
    }

    [Fact]
    [Requirement("CC-DSH-004")]
    public void Refund_WhenAuditAppendFails_DeniesAndNeverMovesMoney()
    {
        audit.Throw = true;

        var result = Service().Refund(DashboardTestData.Staff("finance"), DashboardTestData.OrderRef, "corr-1");

        Assert.Equal(DashboardActionStatus.Unavailable, result.Status);
        Assert.Empty(commands.Refunds);
        Assert.Equal(0, commands.Invocations);
    }

    [Fact]
    [Requirement("CC-DSH-004")]
    public void Refund_WhenOnlyTheOutcomeAppendFails_StillSucceeds()
    {
        // The attempt record — the one that guarantees no unaudited effect —
        // is already durable. A failure appending the outcome must not report
        // failure for an action that really happened.
        audit.ThrowForAction = "orders.refund.applied";

        var result = Service().Refund(DashboardTestData.Staff("finance"), DashboardTestData.OrderRef, "corr-1");

        Assert.Equal(DashboardActionStatus.Completed, result.Status);
        Assert.Single(commands.Refunds);
        Assert.Single(audit.Appended);
        Assert.Equal("orders.refund", audit.Appended[0].Action);
    }

    [Fact]
    [Requirement("CC-DSH-002")]
    public void EveryDenial_CarriesAReason_ForTheStructuredAuthzEvent()
    {
        // AC-06: denials are logged as structured authz events, which needs a
        // reason on every one (SECURITY.md, Logging rule 3).
        var denial = Service().Search(
            DashboardTestData.Staff("sales-viewer"), DashboardOrderSearchQuery.Create(), "corr-1");

        Assert.NotEqual(AccessDenialReason.None, denial.DenialReason);
    }
}
