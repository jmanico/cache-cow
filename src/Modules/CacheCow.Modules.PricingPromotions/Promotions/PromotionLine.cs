using CacheCow.SharedKernel;

namespace CacheCow.Modules.PricingPromotions.Promotions;

/// <summary>
/// One order/display line submitted for promotion evaluation: SKU, unit price
/// (from the canonical price model, issue 032 — never client-supplied,
/// CC-PRC-005), attacker-influenced quantity (validated and overflow-checked,
/// CC-PRC-003), and the SKU's category as opaque Catalog-context input.
/// </summary>
public sealed record PromotionLine
{
    public PromotionLine(SkuId sku, Money unitPrice, long quantity, string? category = null)
    {
        if (sku == default)
        {
            throw new PricingValidationException("A promotion line requires a SKU identity (CC-PRC-006).");
        }

        if (unitPrice.MinorUnits <= 0)
        {
            throw new PricingValidationException(
                "A promotion line requires a positive unit price in minor units (CC-PRC-003).");
        }

        if (quantity <= 0)
        {
            throw new PricingValidationException(
                "A promotion line requires a positive quantity; quantities are attacker-influenced input and are validated, never coerced (CC-PRC-003).");
        }

        if (category is not null && string.IsNullOrWhiteSpace(category))
        {
            throw new PricingValidationException(
                "A promotion line category, when present, must be non-empty (CC-PRC-006).");
        }

        Sku = sku;
        UnitPrice = unitPrice;
        Quantity = quantity;
        Category = category;
    }

    public SkuId Sku { get; }

    public Money UnitPrice { get; }

    public long Quantity { get; }

    public string? Category { get; }
}
