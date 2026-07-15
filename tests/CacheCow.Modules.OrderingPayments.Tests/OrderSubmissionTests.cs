using System.Reflection;
using CacheCow.Modules.OrderingPayments.Orders;
using CacheCow.Modules.OrderingPayments.Submission;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.OrderingPayments.Tests;

/// <summary>
/// Issue 036 (CC-ORD-001, CC-PRC-005): guest checkout is first-class, the
/// client contributes SKU + quantity only, and every monetary value on the
/// resulting order is recomputed server-side from the canonical ports in
/// overflow-checked integer minor units.
/// </summary>
public sealed class OrderSubmissionTests
{
    private static readonly BuyerIdentity Guest = BuyerIdentity.ForGuestSession("guest-session-42");
    private static readonly BuyerIdentity Account = BuyerIdentity.ForAccount("account-97");

    [Fact]
    [Requirement("CC-ORD-001")]
    public void Guest_submission_creates_a_received_order_without_any_account()
    {
        var order = Fixtures.SubmissionService().Submit(
            new OrderSubmissionRequest([new SubmittedCartLine(Fixtures.Brisket, 2)]),
            Guest,
            Market.US);

        Assert.Equal(OrderState.Received, order.State);
        Assert.Equal(BuyerKind.GuestSession, order.Buyer.Kind);
        Assert.Equal("guest-session-42", order.Buyer.Identifier);
        Assert.Equal(Market.US, order.Market);
    }

    [Fact]
    [Requirement("CC-ORD-001")]
    public void Account_submission_is_supported_identically_account_is_optional_not_required()
    {
        var service = Fixtures.SubmissionService();
        var request = new OrderSubmissionRequest([new SubmittedCartLine(Fixtures.Brisket, 1)]);

        var guestOrder = service.Submit(request, Guest, Market.US);
        var accountOrder = service.Submit(request, Account, Market.US);

        Assert.Equal(BuyerKind.GuestSession, guestOrder.Buyer.Kind);
        Assert.Equal(BuyerKind.Account, accountOrder.Buyer.Kind);
        Assert.Equal(guestOrder.GrandTotal, accountOrder.GrandTotal);
    }

    [Fact]
    [Requirement("CC-PRC-005")]
    [Requirement("CC-QA-004")]
    public void Totals_are_recomputed_entirely_from_the_canonical_ports()
    {
        // Unit prices from the price port, discount from the promotion port,
        // tax from the tax port — the request carried quantities only.
        var tax = new StubTaxCalculator((_, taxable) =>
            Money.FromMinorUnits(taxable.MinorUnits / 10, taxable.Currency));
        var promotions = new StubPromotionEvaluator((sku, _, _, lineSubtotal, _) =>
            sku == Fixtures.Ribs
                ? Money.FromMinorUnits(500, lineSubtotal.Currency)
                : Money.FromMinorUnits(0, lineSubtotal.Currency));

        var order = Fixtures.SubmissionService(promotions: promotions, tax: tax).Submit(
            new OrderSubmissionRequest(
            [
                new SubmittedCartLine(Fixtures.Brisket, 2),
                new SubmittedCartLine(Fixtures.Ribs, 3),
            ]),
            Guest,
            Market.US);

        var brisket = Assert.Single(order.Lines, l => l.Sku == Fixtures.Brisket);
        Assert.Equal(4_999, brisket.UnitPrice.MinorUnits);
        Assert.Equal(9_998, brisket.LineSubtotal.MinorUnits);
        Assert.Equal(0, brisket.Discount.MinorUnits);
        Assert.Equal(9_998, brisket.LineTotal.MinorUnits);

        var ribs = Assert.Single(order.Lines, l => l.Sku == Fixtures.Ribs);
        Assert.Equal(8_997, ribs.LineSubtotal.MinorUnits);
        Assert.Equal(500, ribs.Discount.MinorUnits);
        Assert.Equal(8_497, ribs.LineTotal.MinorUnits);

        Assert.Equal(18_995, order.Subtotal.MinorUnits);
        Assert.Equal(500, order.DiscountTotal.MinorUnits);
        Assert.Equal(1_849, order.TaxTotal.MinorUnits); // 10% of 18,495, integer division
        Assert.Equal(20_344, order.GrandTotal.MinorUnits);
        Assert.Equal(Currency.Usd, order.GrandTotal.Currency);
    }

    [Fact]
    [Requirement("CC-PRC-005")]
    [Requirement("CC-QA-004")]
    public void Zero_decimal_and_high_grouping_currencies_stay_in_integer_minor_units()
    {
        var jpyAndInr = new FixedPriceSource()
            .With(Market.JP, Fixtures.Brisket, Money.FromMinorUnits(14_900, Currency.Jpy))
            .With(Market.IN, Fixtures.Paneer, Money.FromMinorUnits(1_24_90_000, Currency.Inr));

        var service = Fixtures.SubmissionService(prices: jpyAndInr);

        var jpOrder = service.Submit(
            new OrderSubmissionRequest([new SubmittedCartLine(Fixtures.Brisket, 3)]),
            Guest,
            Market.JP);
        Assert.Equal(44_700, jpOrder.GrandTotal.MinorUnits);
        Assert.Equal(Currency.Jpy, jpOrder.GrandTotal.Currency);

        var inOrder = service.Submit(
            new OrderSubmissionRequest([new SubmittedCartLine(Fixtures.Paneer, 2)]),
            Guest,
            Market.IN);
        Assert.Equal(2_49_80_000, inOrder.GrandTotal.MinorUnits);
        Assert.Equal(Currency.Inr, inOrder.GrandTotal.Currency);
    }

    [Fact]
    [Requirement("CC-PRC-005")]
    public void Submission_dto_cannot_represent_any_monetary_value()
    {
        // SECURITY.md, Input validation rule 3 / issue 036 AC-03 made
        // structural: the client-bound input types expose no member of any
        // money-capable type, so a client-supplied price, discount, tax, or
        // total is unrepresentable rather than merely ignored.
        var inputTypes = new[] { typeof(OrderSubmissionRequest), typeof(SubmittedCartLine), typeof(BuyerIdentity) };
        var moneyCapable = new[] { typeof(Money), typeof(decimal), typeof(long), typeof(ulong) };

        bool IsMoneyCapable(Type type) =>
            moneyCapable.Contains(type)
            || (Nullable.GetUnderlyingType(type) is { } underlying && moneyCapable.Contains(underlying));

        foreach (var type in inputTypes)
        {
            const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            Assert.All(type.GetProperties(All), p => Assert.False(IsMoneyCapable(p.PropertyType), $"{type.Name}.{p.Name}"));
            Assert.All(type.GetFields(All), f => Assert.False(IsMoneyCapable(f.FieldType), $"{type.Name}.{f.Name}"));
            Assert.All(
                type.GetConstructors(All).SelectMany(c => c.GetParameters()),
                p => Assert.False(IsMoneyCapable(p.ParameterType), $"{type.Name}..ctor({p.Name})"));
        }
    }

    [Fact]
    [Requirement("CC-PRC-005")]
    [Requirement("CC-QA-004")]
    public void Expired_promotion_is_not_applied_even_if_cached_ui_displayed_it()
    {
        // CC-PRC-006: the order service is final authority. The promotion
        // window ends before submission time, so the discount evaluates to
        // zero regardless of what the client saw.
        var expiry = Fixtures.T0 - TimeSpan.FromMinutes(1);
        var promotions = new StubPromotionEvaluator((_, _, _, lineSubtotal, submittedAt) =>
            submittedAt <= expiry
                ? Money.FromMinorUnits(1_000, lineSubtotal.Currency)
                : Money.FromMinorUnits(0, lineSubtotal.Currency));

        var order = Fixtures
            .SubmissionService(promotions: promotions, clock: new ManualTimeProvider(Fixtures.T0))
            .Submit(
                new OrderSubmissionRequest([new SubmittedCartLine(Fixtures.Brisket, 1)]),
                Guest,
                Market.US);

        Assert.Equal(0, order.DiscountTotal.MinorUnits);
        Assert.Equal(4_999, order.GrandTotal.MinorUnits);
    }

    [Theory]
    [Requirement("CC-PRC-005")]
    [InlineData(0, OrderSubmissionRejection.NonPositiveQuantity)]
    [InlineData(-1, OrderSubmissionRejection.NonPositiveQuantity)]
    [InlineData(101, OrderSubmissionRejection.QuantityExceedsMaximum)]
    [InlineData(int.MaxValue, OrderSubmissionRejection.QuantityExceedsMaximum)]
    public void Quantity_bounds_are_enforced(int quantity, OrderSubmissionRejection expected)
    {
        var rejected = Assert.Throws<OrderSubmissionRejectedException>(() =>
            Fixtures.SubmissionService(maxQuantityPerLine: 100).Submit(
                new OrderSubmissionRequest([new SubmittedCartLine(Fixtures.Brisket, quantity)]),
                Guest,
                Market.US));

        Assert.Equal(expected, rejected.Reason);
    }

    [Fact]
    [Requirement("CC-PRC-005")]
    public void Empty_cart_and_duplicate_sku_lines_are_rejected()
    {
        var service = Fixtures.SubmissionService();

        var empty = Assert.Throws<OrderSubmissionRejectedException>(() =>
            service.Submit(new OrderSubmissionRequest([]), Guest, Market.US));
        Assert.Equal(OrderSubmissionRejection.EmptyCart, empty.Reason);

        var duplicate = Assert.Throws<OrderSubmissionRejectedException>(() =>
            service.Submit(
                new OrderSubmissionRequest(
                [
                    new SubmittedCartLine(Fixtures.Brisket, 1),
                    new SubmittedCartLine(Fixtures.Brisket, 2),
                ]),
                Guest,
                Market.US));
        Assert.Equal(OrderSubmissionRejection.DuplicateSku, duplicate.Reason);
    }

    [Fact]
    [Requirement("CC-PRC-005")]
    public void Sku_without_a_canonical_price_in_the_transacting_market_rejects_the_order()
    {
        // Gating parity: a SKU absent from the market's canonical pricing
        // (e.g. gated non-veg in IN, CC-MKT-003) can never be ordered.
        var rejected = Assert.Throws<OrderSubmissionRejectedException>(() =>
            Fixtures.SubmissionService().Submit(
                new OrderSubmissionRequest([new SubmittedCartLine(Fixtures.Brisket, 1)]),
                Guest,
                Market.IN));

        Assert.Equal(OrderSubmissionRejection.SkuUnavailableInTransactingMarket, rejected.Reason);
    }

    [Fact]
    [Requirement("CC-PRC-005")]
    [Requirement("CC-QA-004")]
    public void Attacker_scale_quantities_overflow_fail_closed_and_create_no_order()
    {
        // CC-PRC-003: quantity × unit price is overflow-checked; a quantity
        // engineered to wrap the 64-bit total aborts instead of producing a
        // wrong (possibly tiny) total.
        var expensive = new FixedPriceSource()
            .With(Market.IN, Fixtures.Paneer, Money.FromMinorUnits(10_000_000_000, Currency.Inr));
        var service = Fixtures.SubmissionService(prices: expensive, maxQuantityPerLine: int.MaxValue);

        Assert.Throws<MoneyOverflowException>(() =>
            service.Submit(
                new OrderSubmissionRequest([new SubmittedCartLine(Fixtures.Paneer, 2_000_000_000)]),
                Guest,
                Market.IN));
    }

    [Fact]
    [Requirement("CC-PRC-005")]
    public void Out_of_range_canonical_amounts_fail_closed()
    {
        var negativeDiscount = new StubPromotionEvaluator((_, _, _, lineSubtotal, _) =>
            Money.FromMinorUnits(-1, lineSubtotal.Currency));
        var oversizedDiscount = new StubPromotionEvaluator((_, _, _, lineSubtotal, _) =>
            lineSubtotal.Add(Money.FromMinorUnits(1, lineSubtotal.Currency)));
        var negativeTax = new StubTaxCalculator((_, taxable) =>
            Money.FromMinorUnits(-1, taxable.Currency));

        var request = new OrderSubmissionRequest([new SubmittedCartLine(Fixtures.Brisket, 1)]);

        foreach (var service in new[]
        {
            Fixtures.SubmissionService(promotions: negativeDiscount),
            Fixtures.SubmissionService(promotions: oversizedDiscount),
            Fixtures.SubmissionService(tax: negativeTax),
        })
        {
            var rejected = Assert.Throws<OrderSubmissionRejectedException>(
                () => service.Submit(request, Guest, Market.US));
            Assert.Equal(OrderSubmissionRejection.CanonicalAmountOutOfRange, rejected.Reason);
        }
    }

    [Fact]
    [Requirement("CC-PRC-005")]
    public void Submission_timestamp_comes_from_the_server_clock()
    {
        var clock = new ManualTimeProvider(Fixtures.T0);
        var order = Fixtures.SubmissionService(clock: clock).Submit(
            new OrderSubmissionRequest([new SubmittedCartLine(Fixtures.Brisket, 1)]),
            Guest,
            Market.US);

        Assert.Equal(Fixtures.T0, order.SubmittedAt);
    }
}
