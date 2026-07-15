using CacheCow.SharedKernel;
using CacheCow.Modules.OrderingPayments.Submission;

namespace CacheCow.Modules.OrderingPayments.Orders;

/// <summary>
/// Immutable order aggregate. Created only by
/// <see cref="Submission.OrderSubmissionService"/> in state
/// <see cref="OrderState.Received"/> (CC-ORD-001), carrying the line-item
/// money breakdown the server recomputed from canonical data (CC-PRC-005) in
/// integer minor units (CC-PRC-003). State changes exist only as new instances
/// produced by <see cref="OrderStateMachine.Transition"/> — the constructor
/// and <see cref="WithState"/> are internal, so no code path outside this
/// module's single transition API can change order state (issue 035, AC-06).
/// </summary>
public sealed class Order
{
    internal Order(
        OrderId id,
        BuyerIdentity buyer,
        Market market,
        OrderState state,
        IReadOnlyList<OrderLine> lines,
        Money subtotal,
        Money discountTotal,
        Money taxTotal,
        Money grandTotal,
        DateTimeOffset submittedAt)
    {
        Id = id;
        Buyer = buyer;
        Market = market;
        State = state;
        Lines = lines;
        Subtotal = subtotal;
        DiscountTotal = discountTotal;
        TaxTotal = taxTotal;
        GrandTotal = grandTotal;
        SubmittedAt = submittedAt;
    }

    public OrderId Id { get; }

    /// <summary>Guest session or account — both are first-class (CC-ORD-001).</summary>
    public BuyerIdentity Buyer { get; }

    /// <summary>The server-resolved transacting market (CC-SEC-012); never a client hint.</summary>
    public Market Market { get; }

    public OrderState State { get; }

    public IReadOnlyList<OrderLine> Lines { get; }

    /// <summary>Sum of line subtotals before discounts (server-computed, CC-PRC-005).</summary>
    public Money Subtotal { get; }

    /// <summary>Sum of applied per-line discounts (server-evaluated at submission time, CC-PRC-006).</summary>
    public Money DiscountTotal { get; }

    /// <summary>Tax as returned by the external tax calculator port (Stripe Tax / Razorpay per ARCHITECTURE.md).</summary>
    public Money TaxTotal { get; }

    /// <summary>Subtotal − discounts + tax, all overflow-checked (CC-PRC-003).</summary>
    public Money GrandTotal { get; }

    /// <summary>Server clock at submission (SECURITY.md, Input validation rule 3).</summary>
    public DateTimeOffset SubmittedAt { get; }

    /// <summary>
    /// Internal on purpose: only <see cref="OrderStateMachine"/> may produce a
    /// state change, and only after the audit append succeeded (issue 035, AC-03/AC-06).
    /// </summary>
    internal Order WithState(OrderState state) =>
        new(Id, Buyer, Market, state, Lines, Subtotal, DiscountTotal, TaxTotal, GrandTotal, SubmittedAt);
}

/// <summary>
/// One recomputed order line (CC-PRC-005): every monetary value is
/// server-derived; nothing here ever originates from the submission DTO.
/// </summary>
public sealed record OrderLine(
    SkuId Sku,
    int Quantity,
    Money UnitPrice,
    Money LineSubtotal,
    Money Discount,
    Money LineTotal);
