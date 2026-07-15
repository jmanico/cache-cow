namespace CacheCow.Modules.OrderingPayments.Submission;

/// <summary>Why a submission was rejected before any order was created.</summary>
public enum OrderSubmissionRejection
{
    /// <summary>The submission contained no lines.</summary>
    EmptyCart = 0,

    /// <summary>A line quantity was zero or negative.</summary>
    NonPositiveQuantity = 1,

    /// <summary>A line quantity exceeded the configured maximum (attacker-influenced input, CC-PRC-003).</summary>
    QuantityExceedsMaximum = 2,

    /// <summary>The same SKU appeared on more than one line (ambiguous; rejected rather than merged).</summary>
    DuplicateSku = 3,

    /// <summary>The canonical price source has no price for the SKU in the transacting market (unavailable or gated, CC-MKT-003 parity).</summary>
    SkuUnavailableInTransactingMarket = 4,

    /// <summary>A canonical port returned an out-of-range amount (negative discount/tax, discount above line subtotal); failing closed.</summary>
    CanonicalAmountOutOfRange = 5,
}

/// <summary>
/// Typed rejection of an order submission (maps to HTTP 400 problem details at
/// the API surface, issue 021). No order row exists when this is thrown
/// (issue 036, Failure Behavior).
/// </summary>
public sealed class OrderSubmissionRejectedException : Exception
{
    public OrderSubmissionRejectedException(OrderSubmissionRejection reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    public OrderSubmissionRejection Reason { get; }
}
