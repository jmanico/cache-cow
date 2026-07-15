using CacheCow.Modules.PricingPromotions.Pricing;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.PricingPromotions.Promotions;

/// <summary>
/// A promotion definition as data (CC-PRC-006): one market, percentage or fixed
/// discount, per-SKU or per-category scope, and a start/end window authored as
/// wall-clock timestamps in the market's timezone. Vocabulary is deliberately
/// neutral: clearance is a boolean classification only — presentation naming
/// such as "Eviction Specials" (DESIGN.md §5.3) never appears in engine or
/// invoice-facing data (CC-PRC-007).
/// </summary>
public sealed record Promotion
{
    public Promotion(
        string id,
        Market market,
        Discount discount,
        PromotionScope scope,
        DateTime startAtMarketTime,
        DateTime endAtMarketTime,
        bool isClearance = false,
        StackingPolicy stacking = StackingPolicy.NoStacking)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new PricingValidationException("A promotion requires a non-empty identifier (CC-PRC-006).");
        }

        ArgumentNullException.ThrowIfNull(discount);
        ArgumentNullException.ThrowIfNull(scope);

        var marketCurrency = LaunchMarketCurrencies.CurrencyOf(market);
        if (discount is FixedAmountPerUnitDiscount fixedAmount
            && !fixedAmount.AmountPerUnit.Currency.Equals(marketCurrency))
        {
            throw new PricingValidationException(
                $"A {market.Code} promotion's fixed discount must be denominated in {marketCurrency.Code}, not {fixedAmount.AmountPerUnit.Currency.Code} (CC-PRC-001; no runtime FX conversion exists).");
        }

        if (startAtMarketTime.Kind != DateTimeKind.Unspecified || endAtMarketTime.Kind != DateTimeKind.Unspecified)
        {
            throw new PricingValidationException(
                "Promotion windows are wall-clock timestamps in the market's timezone (CC-PRC-006); pass DateTimeKind.Unspecified values, never UTC- or machine-local-kinded ones.");
        }

        if (endAtMarketTime <= startAtMarketTime)
        {
            throw new PricingValidationException(
                "A promotion window must end after it starts (CC-PRC-006; issue 033 AC-07).");
        }

        if (stacking != StackingPolicy.NoStacking)
        {
            throw new PricingValidationException(
                "Only the no-stacking default is implemented; non-default stacking semantics are unspecified and await human ratification (CC-PRC-006; issue 033, Open Questions).");
        }

        Id = id;
        Market = market;
        Discount = discount;
        Scope = scope;
        StartAtMarketTime = startAtMarketTime;
        EndAtMarketTime = endAtMarketTime;
        IsClearance = isClearance;
        Stacking = stacking;
    }

    /// <summary>Neutral engine identifier; never a brand or presentation name (CC-PRC-007).</summary>
    public string Id { get; }

    /// <summary>The single market the promotion applies in (CC-PRC-006).</summary>
    public Market Market { get; }

    public Discount Discount { get; }

    public PromotionScope Scope { get; }

    /// <summary>Inclusive start, wall-clock in the market timezone (CC-PRC-006).</summary>
    public DateTime StartAtMarketTime { get; }

    /// <summary>Exclusive end, wall-clock in the market timezone: the promotion stops applying at exactly this instant (issue 033 AC-02).</summary>
    public DateTime EndAtMarketTime { get; }

    /// <summary>
    /// Neutral clearance classification. Presentation layers may render
    /// clearance promotions under their own naming (DESIGN.md §5.3); that
    /// naming is presentation-only and never flows from or into this model
    /// (CC-PRC-007).
    /// </summary>
    public bool IsClearance { get; }

    /// <summary>Always <see cref="StackingPolicy.NoStacking"/> until non-default semantics are ratified.</summary>
    public StackingPolicy Stacking { get; }

    /// <summary>
    /// Window test against a wall-clock instant already converted to the
    /// market's timezone: [start, end) — active at the start instant, no longer
    /// active at the end instant (issue 033 AC-02).
    /// </summary>
    public bool IsActiveAt(DateTime marketLocalTime)
    {
        if (marketLocalTime.Kind != DateTimeKind.Unspecified)
        {
            throw new PricingValidationException(
                "Window evaluation expects a wall-clock time already converted to the market timezone (CC-PRC-006).");
        }

        return StartAtMarketTime <= marketLocalTime && marketLocalTime < EndAtMarketTime;
    }
}
