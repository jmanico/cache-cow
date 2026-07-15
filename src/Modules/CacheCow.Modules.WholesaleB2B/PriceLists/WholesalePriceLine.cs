using CacheCow.SharedKernel;

namespace CacheCow.Modules.WholesaleB2B.PriceLists;

/// <summary>
/// One case-quantity price row (CC-WHS-001): a SKU sold in cases of
/// <see cref="CasePackSize"/> units at <see cref="PricePerCase"/>, integer
/// minor units via the shared <see cref="Money"/> type (CC-PRC-003;
/// ARCHITECTURE.md, Dependency rule 9). Whether price lists also carry volume
/// tiers or lead-time data is an open decision (issue 050, Open Questions), so
/// the row is deliberately minimal. Display of prices is downstream (issue 052);
/// only the Ordering path computes order money at submission (ARCHITECTURE.md,
/// Dependency rule 2) — <see cref="ExtendedPrice"/> exists for that
/// recomputation and for invoicing, with overflow-checked arithmetic that fails
/// closed on attacker-influenced case counts (CC-PRC-003).
/// </summary>
public sealed record WholesalePriceLine
{
    public WholesalePriceLine(SkuId sku, int casePackSize, Money pricePerCase)
    {
        if (sku == default)
        {
            throw new WholesaleValidationException(
                "A wholesale price line requires a SKU identity (CC-WHS-001).");
        }

        if (casePackSize <= 0)
        {
            throw new WholesaleValidationException(
                "A wholesale price line requires a positive case pack size (units per case) (CC-WHS-001).");
        }

        if (pricePerCase.MinorUnits <= 0)
        {
            throw new WholesaleValidationException(
                "A wholesale case price must be a positive amount of minor units; zero and negative prices are rejected, never defaulted (CC-WHS-001, CC-PRC-003).");
        }

        Sku = sku;
        CasePackSize = casePackSize;
        PricePerCase = pricePerCase;
    }

    public SkuId Sku { get; }

    /// <summary>Units per case — wholesale ordering is case-quantity (CC-WHS-001).</summary>
    public int CasePackSize { get; }

    /// <summary>Per-case price, integer minor units in the owning list's market currency (CC-PRC-001/003).</summary>
    public Money PricePerCase { get; }

    /// <summary>
    /// Price of <paramref name="caseCount"/> cases. Case counts are
    /// attacker-influenced quantities: the multiplication is overflow-checked
    /// and fails closed (CC-PRC-003).
    /// </summary>
    public Money ExtendedPrice(long caseCount) =>
        caseCount > 0
            ? PricePerCase.MultiplyBy(caseCount)
            : throw new WholesaleValidationException(
                "An extended wholesale price requires a positive case count (CC-WHS-001, CC-PRC-003).");
}
