using CacheCow.Modules.PricingPromotions.Pricing;
using CacheCow.Modules.PricingPromotions.Rounding;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.PricingPromotions.Promotions;

/// <summary>
/// Promotion evaluation engine (CC-PRC-006). Trusts only the injected
/// <see cref="TimeProvider"/> (the authoritative server clock), the server-side
/// promotion records in the request, and the server-side transacting market —
/// never client timestamps or cached UI state. All money arithmetic goes
/// through the shared <see cref="Money"/> type and exact integer scaling;
/// overflow fails closed (CC-PRC-003).
/// </summary>
public sealed class PromotionEvaluator : IPromotionEvaluator
{
    private const long BasisPointsPerWhole = 10_000;

    private readonly IMarketTimeZoneProvider _timeZones;
    private readonly TimeProvider _clock;

    public PromotionEvaluator(IMarketTimeZoneProvider timeZones, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(timeZones);
        ArgumentNullException.ThrowIfNull(clock);
        _timeZones = timeZones;
        _clock = clock;
    }

    public PromotionEvaluationResult Evaluate(PromotionEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var marketCurrency = LaunchMarketCurrencies.CurrencyOf(request.Market);
        MinorUnitArithmetic.RequireSpecified(request.Rounding);

        // Resolved lazily: a request with no candidate promotions needs no zone.
        DateTime? marketNow = null;

        var lines = new List<LinePricing>(request.Lines.Count);
        var total = Money.FromMinorUnits(0, marketCurrency);

        foreach (var line in request.Lines)
        {
            if (!line.UnitPrice.Currency.Equals(marketCurrency))
            {
                throw new PricingValidationException(
                    $"Line for SKU '{line.Sku.Value}' is priced in {line.UnitPrice.Currency.Code} but the transacting market {request.Market.Code} uses {marketCurrency.Code} (CC-PRC-001; no runtime FX conversion exists).");
            }

            // Overflow-checked: attacker-scale quantities fail closed (CC-PRC-003).
            var subtotal = line.UnitPrice * line.Quantity;

            Promotion? applied = null;
            var appliedDiscountMinorUnits = 0L;

            foreach (var promotion in request.Promotions)
            {
                // Per-market isolation (issue 033 AC-01).
                if (promotion.Market != request.Market)
                {
                    continue;
                }

                if (!promotion.Scope.Matches(line.Sku, line.Category))
                {
                    continue;
                }

                marketNow ??= ResolveMarketNow(request.Market);
                if (!promotion.IsActiveAt(marketNow.Value))
                {
                    // Expired or not yet started: never applies, regardless of
                    // any cached UI that still displays it (CC-PRC-006 AC-03).
                    continue;
                }

                var discountMinorUnits = ComputeDiscountMinorUnits(promotion.Discount, line, subtotal, request.Rounding);
                if (discountMinorUnits <= 0)
                {
                    continue;
                }

                // No stacking: exactly one promotion applies per line
                // (CC-PRC-006 AC-04). Selection among several applicable
                // promotions is unspecified by the specs; this engine applies
                // the deterministic derived rule "greatest discount wins, ties
                // broken by ordinal promotion ID" — flagged for ratification
                // (issue 033, Open Questions).
                if (applied is null
                    || discountMinorUnits > appliedDiscountMinorUnits
                    || (discountMinorUnits == appliedDiscountMinorUnits
                        && string.CompareOrdinal(promotion.Id, applied.Id) < 0))
                {
                    applied = promotion;
                    appliedDiscountMinorUnits = discountMinorUnits;
                }
            }

            var discount = Money.FromMinorUnits(appliedDiscountMinorUnits, marketCurrency);
            var lineTotal = subtotal - discount;
            total += lineTotal;

            lines.Add(new LinePricing(line.Sku, line.Quantity, subtotal, discount, lineTotal, applied?.Id));
        }

        return new PromotionEvaluationResult(request.Market, lines, total);
    }

    /// <summary>
    /// Computes the promotion's discount for a line in integer minor units,
    /// clamped to the line subtotal so a discount can never drive a line or
    /// total negative (issue 033 AC-05).
    /// </summary>
    private static long ComputeDiscountMinorUnits(
        Discount discount, PromotionLine line, Money subtotal, RoundingMode rounding)
    {
        var raw = discount switch
        {
            PercentageDiscount percentage => MinorUnitArithmetic.MultiplyAndDivide(
                subtotal.MinorUnits, percentage.BasisPoints, BasisPointsPerWhole, rounding),
            FixedAmountPerUnitDiscount fixedAmount =>
                // Overflow-checked multiplication; failure denies the discount
                // rather than wrapping (CC-PRC-003, fail closed).
                (fixedAmount.AmountPerUnit * line.Quantity).MinorUnits,
            _ => throw new PricingValidationException(
                "Unrecognized discount kind; failing closed — a failure never grants a discount (SECURITY.md, Logging rule 2)."),
        };

        return Math.Min(raw, subtotal.MinorUnits);
    }

    private DateTime ResolveMarketNow(Market market)
    {
        if (!_timeZones.TryGetTimeZone(market, out var zone) || zone is null)
        {
            throw new MarketTimeZoneUnavailableException(market);
        }

        // Instant -> market wall clock is total (no DST ambiguity), so window
        // comparison happens in the market's own timezone (issue 033 AC-02).
        return TimeZoneInfo.ConvertTime(_clock.GetUtcNow(), zone).DateTime;
    }
}
