using CacheCow.Modules.PricingPromotions.Rounding;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.PricingPromotions.Promotions;

/// <summary>
/// Input to authoritative promotion evaluation (CC-PRC-006): the server-side
/// transacting market, the lines being priced, the candidate promotion records
/// (server data — never client-claimed terms), and the explicit rounding mode
/// for percentage discounts (unratified policy; issue 033, Open Questions —
/// there is deliberately no default).
/// </summary>
public sealed record PromotionEvaluationRequest
{
    public PromotionEvaluationRequest(
        Market market,
        IReadOnlyList<PromotionLine> lines,
        IReadOnlyList<Promotion> promotions,
        RoundingMode rounding)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(promotions);

        if (lines.Any(line => line is null))
        {
            throw new PricingValidationException("Promotion evaluation lines must be non-null (CC-PRC-006).");
        }

        if (promotions.Any(promotion => promotion is null))
        {
            throw new PricingValidationException("Candidate promotions must be non-null (CC-PRC-006).");
        }

        Market = market;
        Lines = lines.ToArray();
        Promotions = promotions.ToArray();
        Rounding = rounding;
    }

    /// <summary>Server-side transacting market (CC-SEC-012: never a client hint).</summary>
    public Market Market { get; }

    public IReadOnlyList<PromotionLine> Lines { get; }

    /// <summary>Candidate promotion records from server state; expired ones are filtered by the evaluator's clock, not trusted from cached UI (CC-PRC-006).</summary>
    public IReadOnlyList<Promotion> Promotions { get; }

    /// <summary>Explicit rounding mode for discount scaling; unspecified values are rejected (unratified policy).</summary>
    public RoundingMode Rounding { get; }
}
