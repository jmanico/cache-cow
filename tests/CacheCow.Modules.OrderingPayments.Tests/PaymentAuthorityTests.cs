using CacheCow.Modules.OrderingPayments.Orders;
using CacheCow.Modules.OrderingPayments.Payments;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.OrderingPayments.Tests;

/// <summary>
/// Issue 041 (CC-ORD-009): payment/order state advances ONLY from a
/// signature-verified processor event AND an agreeing server-initiated
/// reconciliation check — mismatch, unknown status, or reconciliation failure
/// means no advancement, fail closed, and the advancement itself is an
/// audited state-machine transition.
/// </summary>
[Requirement("CC-ORD-009")]
[Requirement("CC-SEC-014")]
public sealed class PaymentAuthorityTests
{
    private static PaymentAuthorityService Service(
        IProcessorStatusClient statusClient,
        RecordingAuditSink? audit = null) =>
        new(statusClient, new OrderStateMachine(audit ?? new RecordingAuditSink()));

    [Fact]
    public void Verified_event_plus_paid_reconciliation_advances_received_to_confirmed()
    {
        // AC-01/AC-04: both authorities agree -> the order confirms, through
        // the audited state machine (CC-ORD-006).
        var audit = new RecordingAuditSink();
        var statusClient = new StubProcessorStatusClient(ProcessorPaymentStatus.Paid);
        var service = Service(statusClient, audit);
        var order = Fixtures.NewReceivedOrder();

        var confirmed = service.ConfirmPayment(order, Fixtures.VerifiedEvent(), "pay_ref_1");

        Assert.Equal(OrderState.Confirmed, confirmed.State);
        Assert.Equal(1, statusClient.Calls);
        var auditEvent = Assert.Single(audit.Events);
        Assert.Equal(OrderState.Received, auditEvent.FromState);
        Assert.Equal(OrderState.Confirmed, auditEvent.ToState);
        Assert.Equal(PaymentAuthorityService.WebhookActorPrefix + Fixtures.StripeProcessor, auditEvent.Actor);
    }

    [Theory]
    [InlineData(ProcessorPaymentStatus.NotPaid)]
    [InlineData(ProcessorPaymentStatus.Unknown)]
    public void Reconciliation_disagreement_denies_and_leaves_order_unchanged(ProcessorPaymentStatus status)
    {
        // AC-04 negative: a verified webhook claiming "paid" is NOT enough
        // when the server-initiated status check does not agree.
        var audit = new RecordingAuditSink();
        var service = Service(new StubProcessorStatusClient(status), audit);
        var order = Fixtures.NewReceivedOrder();

        var denied = Assert.Throws<PaymentNotReconciledException>(
            () => service.ConfirmPayment(order, Fixtures.VerifiedEvent(), "pay_ref_1"));

        Assert.Equal(status, denied.ReportedStatus);
        Assert.Equal(OrderState.Received, order.State);
        Assert.Empty(audit.Events); // no transition, no audit record
    }

    [Fact]
    public void Reconciliation_failure_fails_closed_with_no_state_change()
    {
        // AC-08: an exception in the reconciliation path is a denial.
        var audit = new RecordingAuditSink();
        var service = Service(new ThrowingProcessorStatusClient(), audit);
        var order = Fixtures.NewReceivedOrder();

        Assert.Throws<InvalidOperationException>(
            () => service.ConfirmPayment(order, Fixtures.VerifiedEvent(), "pay_ref_1"));

        Assert.Equal(OrderState.Received, order.State);
        Assert.Empty(audit.Events);
    }

    [Fact]
    public void Audit_failure_denies_the_confirmation()
    {
        // The state machine's audit-first contract holds on this path too
        // (SECURITY.md, Logging rules 2 and 6).
        var service = new PaymentAuthorityService(
            new StubProcessorStatusClient(ProcessorPaymentStatus.Paid),
            new OrderStateMachine(new ThrowingAuditSink()));
        var order = Fixtures.NewReceivedOrder();

        Assert.Throws<InvalidOperationException>(
            () => service.ConfirmPayment(order, Fixtures.VerifiedEvent(), "pay_ref_1"));

        Assert.Equal(OrderState.Received, order.State);
    }

    [Fact]
    public void Confirmation_requires_a_verified_event_and_a_payment_reference()
    {
        // The only accepted proof-of-webhook is the verifier-produced type;
        // null or blank inputs never reach the status client (fail closed).
        var statusClient = new StubProcessorStatusClient(ProcessorPaymentStatus.Paid);
        var service = Service(statusClient);
        var order = Fixtures.NewReceivedOrder();

        Assert.Throws<ArgumentNullException>(() => service.ConfirmPayment(order, null!, "pay_ref_1"));
        Assert.Throws<ArgumentException>(() => service.ConfirmPayment(order, Fixtures.VerifiedEvent(), " "));
        Assert.Equal(0, statusClient.Calls);
        Assert.Equal(OrderState.Received, order.State);
    }

    [Fact]
    public void Already_confirmed_order_cannot_be_confirmed_again_by_a_replayed_flow()
    {
        // Even if a duplicate verified event slips past nonce bounds, the
        // state machine denies Received->Confirmed from Confirmed.
        var service = Service(new StubProcessorStatusClient(ProcessorPaymentStatus.Paid));
        var order = Fixtures.NewReceivedOrder();
        var confirmed = service.ConfirmPayment(order, Fixtures.VerifiedEvent("evt_a"), "pay_ref_1");

        Assert.Throws<IllegalOrderTransitionException>(
            () => service.ConfirmPayment(confirmed, Fixtures.VerifiedEvent("evt_b"), "pay_ref_1"));
    }
}
