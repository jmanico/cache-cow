namespace CacheCow.Modules.OrderingPayments.Payments;

/// <summary>Payment status as reported by a server-initiated status check to the processor (CC-ORD-009).</summary>
public enum ProcessorPaymentStatus
{
    /// <summary>Status could not be positively established — treated exactly like not paid (fail closed).</summary>
    Unknown = 0,

    /// <summary>The processor confirms the payment as captured/paid.</summary>
    Paid = 1,

    /// <summary>The processor reports the payment as not completed (pending, failed, cancelled).</summary>
    NotPaid = 2,
}

/// <summary>
/// Port for the server-initiated confirmation call to the payment processor
/// (Stripe/Razorpay adapters are issues 039/040). This is the reconciliation
/// leg of CC-ORD-009: a signature-verified webhook alone never moves payment
/// or order state — the platform independently asks the processor before
/// anything advances (SECURITY.md, Input validation rule 11).
///
/// Contract: return <see cref="ProcessorPaymentStatus.Paid"/> only on a
/// positive, current answer from the processor's API; throw on transport or
/// processor unavailability — callers fail closed and deny the advancement
/// (issue 041, Failure Behavior; the retry/backoff policy is an open
/// question, issue 041).
/// </summary>
public interface IProcessorStatusClient
{
    /// <summary>Server-initiated status check for one payment at the named processor.</summary>
    ProcessorPaymentStatus GetPaymentStatus(string processorName, string paymentReference);
}
