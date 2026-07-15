namespace CacheCow.Modules.OrderingPayments.Payments;

/// <summary>
/// The signature-verified webhook and the server-initiated status check did
/// not both agree the payment is paid — so nothing advanced (CC-ORD-009;
/// issue 041, AC-04). Payment confirmation requires BOTH sources; a mismatch
/// is a denial and a security-relevant event for the host to log
/// (SECURITY.md, Logging rule 3). No payment reference or payload content is
/// carried here (Logging rule 4).
/// </summary>
public sealed class PaymentNotReconciledException : Exception
{
    public PaymentNotReconciledException(string processorName, ProcessorPaymentStatus reportedStatus)
        : base(
            $"Payment not reconciled: processor '{processorName}' status check returned {reportedStatus}, "
            + "not Paid; order state is unchanged (CC-ORD-009).")
    {
        ProcessorName = processorName;
        ReportedStatus = reportedStatus;
    }

    public string ProcessorName { get; }

    public ProcessorPaymentStatus ReportedStatus { get; }
}
