using CacheCow.Modules.OrderingPayments.Orders;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.OrderingPayments.Submission;

/// <summary>
/// Server-side order submission (issue 036). The client contributes SKU ids
/// and quantities only (<see cref="OrderSubmissionRequest"/>); unit prices,
/// promotions, tax, and totals are recomputed here entirely from the canonical
/// ports at submission time (CC-PRC-005; ARCHITECTURE.md, Dependency rule 2).
/// Buyer identity and transacting market are server-derived parameters, never
/// request fields (SECURITY.md, Input validation rule 3; CC-SEC-012).
///
/// All arithmetic uses the shared-kernel <see cref="Money"/> type: integer
/// minor units, overflow-checked, fail-closed — an attacker-influenced
/// quantity that would overflow aborts the submission with
/// <see cref="MoneyOverflowException"/> and no order exists (CC-PRC-003).
/// Guest checkout is supported identically to account checkout; account
/// creation is never required (CC-ORD-001).
///
/// Duplicate-submission protection wraps this service via the idempotency
/// service (issue 037); payment initiation is issues 039/040 and never happens
/// here.
/// </summary>
public sealed class OrderSubmissionService
{
    private readonly ICanonicalPriceSource _priceSource;
    private readonly IPromotionEvaluator _promotionEvaluator;
    private readonly ITaxCalculator _taxCalculator;
    private readonly OrderSubmissionOptions _options;
    private readonly TimeProvider _timeProvider;

    public OrderSubmissionService(
        ICanonicalPriceSource priceSource,
        IPromotionEvaluator promotionEvaluator,
        ITaxCalculator taxCalculator,
        OrderSubmissionOptions options,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(priceSource);
        ArgumentNullException.ThrowIfNull(promotionEvaluator);
        ArgumentNullException.ThrowIfNull(taxCalculator);
        ArgumentNullException.ThrowIfNull(options);

        _priceSource = priceSource;
        _promotionEvaluator = promotionEvaluator;
        _taxCalculator = taxCalculator;
        _options = options;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Validates the submitted lines, recomputes all money from canonical
    /// data, and returns the immutable order in state
    /// <see cref="OrderState.Received"/>. Throws
    /// <see cref="OrderSubmissionRejectedException"/> (no order created) on
    /// any validation failure, and lets <see cref="MoneyException"/>s from
    /// checked arithmetic propagate (fail closed, CC-PRC-003).
    /// </summary>
    /// <param name="request">Client-bound DTO: SKU + quantity lines only.</param>
    /// <param name="buyer">Server-derived buyer identity (guest session or account, CC-ORD-001).</param>
    /// <param name="transactingMarket">Server-resolved transacting market (CC-SEC-012), never a client hint.</param>
    public Order Submit(OrderSubmissionRequest request, BuyerIdentity buyer, Market transactingMarket)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(buyer);

        ValidateLines(request.Lines);

        var submittedAt = _timeProvider.GetUtcNow();
        var lines = new List<OrderLine>(request.Lines.Count);

        Money? subtotal = null;
        Money? discountTotal = null;

        foreach (var submitted in request.Lines)
        {
            if (!_priceSource.TryGetUnitPrice(submitted.Sku, transactingMarket, out var unitPrice))
            {
                throw new OrderSubmissionRejectedException(
                    OrderSubmissionRejection.SkuUnavailableInTransactingMarket,
                    $"SKU '{submitted.Sku}' has no canonical price in market {transactingMarket} (CC-PRC-001; gating per CC-MKT-003).");
            }

            var lineSubtotal = unitPrice.MultiplyBy(submitted.Quantity);
            var discount = _promotionEvaluator.EvaluateDiscount(
                submitted.Sku, transactingMarket, submitted.Quantity, lineSubtotal, submittedAt);

            var zero = Money.FromMinorUnits(0, unitPrice.Currency);
            if (discount < zero || discount > lineSubtotal)
            {
                throw new OrderSubmissionRejectedException(
                    OrderSubmissionRejection.CanonicalAmountOutOfRange,
                    $"Promotion discount for SKU '{submitted.Sku}' is outside [0, line subtotal]; failing closed (CC-PRC-005/006).");
            }

            var lineTotal = lineSubtotal.Subtract(discount);
            lines.Add(new OrderLine(submitted.Sku, submitted.Quantity, unitPrice, lineSubtotal, discount, lineTotal));

            subtotal = subtotal is { } s ? s.Add(lineSubtotal) : lineSubtotal;
            discountTotal = discountTotal is { } d ? d.Add(discount) : discount;
        }

        // Non-null: ValidateLines guarantees at least one line.
        var orderSubtotal = subtotal!.Value;
        var orderDiscountTotal = discountTotal!.Value;
        var taxableTotal = orderSubtotal.Subtract(orderDiscountTotal);

        var tax = _taxCalculator.CalculateTax(transactingMarket, taxableTotal);
        if (tax < Money.FromMinorUnits(0, taxableTotal.Currency))
        {
            throw new OrderSubmissionRejectedException(
                OrderSubmissionRejection.CanonicalAmountOutOfRange,
                $"Tax calculator returned a negative amount for market {transactingMarket}; failing closed.");
        }

        var grandTotal = taxableTotal.Add(tax);

        return new Order(
            OrderId.New(),
            buyer,
            transactingMarket,
            OrderState.Received,
            lines,
            orderSubtotal,
            orderDiscountTotal,
            tax,
            grandTotal,
            submittedAt);
    }

    private void ValidateLines(IReadOnlyList<SubmittedCartLine> lines)
    {
        if (lines is null || lines.Count == 0)
        {
            throw new OrderSubmissionRejectedException(
                OrderSubmissionRejection.EmptyCart,
                "An order submission must contain at least one line.");
        }

        var seen = new HashSet<SkuId>();
        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
            {
                throw new OrderSubmissionRejectedException(
                    OrderSubmissionRejection.NonPositiveQuantity,
                    $"Quantity for SKU '{line.Sku}' must be positive.");
            }

            if (line.Quantity > _options.MaxQuantityPerLine)
            {
                throw new OrderSubmissionRejectedException(
                    OrderSubmissionRejection.QuantityExceedsMaximum,
                    $"Quantity for SKU '{line.Sku}' exceeds the configured per-line maximum of {_options.MaxQuantityPerLine} (CC-PRC-003).");
            }

            if (!seen.Add(line.Sku))
            {
                throw new OrderSubmissionRejectedException(
                    OrderSubmissionRejection.DuplicateSku,
                    $"SKU '{line.Sku}' appears on more than one line; submit one line per SKU.");
            }
        }
    }
}
