using CacheCow.Modules.OrderingPayments.Idempotency;
using CacheCow.Modules.OrderingPayments.Orders;
using CacheCow.Modules.OrderingPayments.Submission;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.OrderingPayments.Tests;

/// <summary>Deterministic, manually advanced clock (server-controlled time in tests without wall-clock flake).</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    internal ManualTimeProvider(DateTimeOffset start) => _utcNow = start;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    internal void Advance(TimeSpan by) => _utcNow += by;
}

/// <summary>Records every appended audit event, in order.</summary>
internal sealed class RecordingAuditSink : IAuditSink
{
    internal List<OrderAuditEvent> Events { get; } = [];

    public void Append(OrderAuditEvent auditEvent) => Events.Add(auditEvent);
}

/// <summary>Simulates an unavailable append-only audit store (issue 035, AC-03).</summary>
internal sealed class ThrowingAuditSink : IAuditSink
{
    public void Append(OrderAuditEvent auditEvent) =>
        throw new InvalidOperationException("audit store unavailable");
}

/// <summary>Canonical price fixture: (market, sku) -> unit price. Anything unlisted is unavailable in that market.</summary>
internal sealed class FixedPriceSource : ICanonicalPriceSource
{
    private readonly Dictionary<(string Market, SkuId Sku), Money> _prices = [];

    internal int Lookups { get; private set; }

    internal FixedPriceSource With(Market market, SkuId sku, Money unitPrice)
    {
        _prices[(market.Code, sku)] = unitPrice;
        return this;
    }

    public bool TryGetUnitPrice(SkuId sku, Market market, out Money unitPrice)
    {
        Lookups++;
        return _prices.TryGetValue((market.Code, sku), out unitPrice);
    }
}

/// <summary>Delegate-backed promotion evaluator double.</summary>
internal sealed class StubPromotionEvaluator : IPromotionEvaluator
{
    private readonly Func<SkuId, Market, int, Money, DateTimeOffset, Money> _evaluate;

    internal StubPromotionEvaluator(Func<SkuId, Market, int, Money, DateTimeOffset, Money> evaluate) =>
        _evaluate = evaluate;

    internal static StubPromotionEvaluator None { get; } =
        new((_, _, _, lineSubtotal, _) => Money.FromMinorUnits(0, lineSubtotal.Currency));

    public Money EvaluateDiscount(SkuId sku, Market market, int quantity, Money lineSubtotal, DateTimeOffset submittedAt) =>
        _evaluate(sku, market, quantity, lineSubtotal, submittedAt);
}

/// <summary>Delegate-backed external tax calculator double.</summary>
internal sealed class StubTaxCalculator : ITaxCalculator
{
    private readonly Func<Market, Money, Money> _calculate;

    internal StubTaxCalculator(Func<Market, Money, Money> calculate) => _calculate = calculate;

    internal static StubTaxCalculator Zero { get; } =
        new((_, taxable) => Money.FromMinorUnits(0, taxable.Currency));

    public Money CalculateTax(Market market, Money taxableTotal) => _calculate(market, taxableTotal);
}

/// <summary>Simulates an unavailable idempotency store: enforcement must fail closed (issue 037).</summary>
internal sealed class ThrowingIdempotencyStore : IIdempotencyStore
{
    public IdempotencyClaim Claim(IdempotencyScope scope, IdempotencyKey key, RequestFingerprint fingerprint) =>
        throw new InvalidOperationException("idempotency store unavailable");

    public void Complete(IdempotencyScope scope, IdempotencyKey key, object result) =>
        throw new InvalidOperationException("idempotency store unavailable");

    public void Release(IdempotencyScope scope, IdempotencyKey key) =>
        throw new InvalidOperationException("idempotency store unavailable");
}

/// <summary>Builders for a minimal working submission pipeline.</summary>
internal static class Fixtures
{
    internal static readonly SkuId Brisket = SkuId.Parse("SKU-BRISKET");
    internal static readonly SkuId Ribs = SkuId.Parse("SKU-RIBS");
    internal static readonly SkuId Paneer = SkuId.Parse("SKU-PANEER");

    internal static readonly DateTimeOffset T0 = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    internal static FixedPriceSource UsPrices() => new FixedPriceSource()
        .With(Market.US, Brisket, Money.FromMinorUnits(4_999, Currency.Usd))
        .With(Market.US, Ribs, Money.FromMinorUnits(2_999, Currency.Usd));

    internal static OrderSubmissionService SubmissionService(
        ICanonicalPriceSource? prices = null,
        IPromotionEvaluator? promotions = null,
        ITaxCalculator? tax = null,
        int maxQuantityPerLine = 100,
        TimeProvider? clock = null) =>
        new(
            prices ?? UsPrices(),
            promotions ?? StubPromotionEvaluator.None,
            tax ?? StubTaxCalculator.Zero,
            new OrderSubmissionOptions(maxQuantityPerLine),
            clock ?? new ManualTimeProvider(T0));

    /// <summary>A fresh order in state received (the only way any test obtains an order: the submission API).</summary>
    internal static Order NewReceivedOrder() =>
        SubmissionService().Submit(
            new OrderSubmissionRequest([new SubmittedCartLine(Brisket, 1)]),
            BuyerIdentity.ForGuestSession("guest-session-1"),
            Market.US);
}
