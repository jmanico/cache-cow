using CacheCow.Modules.OrderingPayments.Orders;
using CacheCow.Modules.OrderingPayments.Webhooks;

namespace CacheCow.Modules.OrderingPayments.Payments;

/// <summary>
/// The single domain operation that advances an order on payment
/// confirmation (issue 041; CC-ORD-009; SECURITY.md, Input validation
/// rule 11). Authority requires BOTH, by construction:
///
/// 1. a <see cref="VerifiedProcessorEvent"/> — obtainable only from
///    <see cref="WebhookVerifier"/>, so an unverified callback cannot even be
///    passed in; and
/// 2. agreement from the server-initiated status check
///    (<see cref="IProcessorStatusClient"/>) that the payment is
///    <see cref="ProcessorPaymentStatus.Paid"/>.
///
/// A browser redirect back from a payment flow has no representation in this
/// module at all — there is no type for it and no overload accepting one —
/// so "confirm on redirect" is unrepresentable, not merely forbidden
/// (CC-ORD-009, AC-05). On reconciliation mismatch or any exception from the
/// status check, nothing advances (fail closed; issue 041, AC-04/AC-08). The
/// state transition itself goes through <see cref="OrderStateMachine"/>, so
/// it is audit-logged like every other transition (CC-ORD-006).
/// </summary>
public sealed class PaymentAuthorityService
{
    /// <summary>Audit actor prefix for webhook-driven payment confirmations (SECURITY.md, Logging rule 6).</summary>
    public const string WebhookActorPrefix = "processor-webhook:";

    private readonly IProcessorStatusClient _statusClient;
    private readonly OrderStateMachine _stateMachine;

    public PaymentAuthorityService(IProcessorStatusClient statusClient, OrderStateMachine stateMachine)
    {
        ArgumentNullException.ThrowIfNull(statusClient);
        ArgumentNullException.ThrowIfNull(stateMachine);
        _statusClient = statusClient;
        _stateMachine = stateMachine;
    }

    /// <summary>
    /// Confirms payment for <paramref name="order"/> and advances it
    /// <see cref="OrderState.Received"/> → <see cref="OrderState.Confirmed"/>
    /// — only when the verified event's processor, asked directly via the
    /// server-initiated status check, reports the payment as paid. Throws
    /// <see cref="PaymentNotReconciledException"/> on any other status;
    /// exceptions from the status check propagate. In both cases the order is
    /// unchanged.
    /// </summary>
    /// <param name="order">The order in its current persisted state.</param>
    /// <param name="verifiedEvent">The signature-verified processor callback claiming payment (producer: <see cref="WebhookVerifier"/> only).</param>
    /// <param name="paymentReference">Server-held processor payment reference for the reconciliation call (issues 039/040 own its wire meaning).</param>
    public Order ConfirmPayment(Order order, VerifiedProcessorEvent verifiedEvent, string paymentReference)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(verifiedEvent);
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentReference);

        var status = _statusClient.GetPaymentStatus(verifiedEvent.ProcessorName, paymentReference);
        if (status != ProcessorPaymentStatus.Paid)
        {
            throw new PaymentNotReconciledException(verifiedEvent.ProcessorName, status);
        }

        return _stateMachine.Transition(
            order,
            OrderState.Confirmed,
            WebhookActorPrefix + verifiedEvent.ProcessorName);
    }
}
