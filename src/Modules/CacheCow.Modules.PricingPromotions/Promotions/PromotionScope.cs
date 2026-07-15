using CacheCow.SharedKernel;

namespace CacheCow.Modules.PricingPromotions.Promotions;

/// <summary>
/// What a promotion applies to: exactly one SKU or exactly one category
/// (CC-PRC-006 "per-SKU or per-category scope"). Category identifiers are
/// opaque strings owned by the Catalog context (cut/category, CC-CAT-001);
/// evaluation receives each line's category as input rather than reaching
/// across the module boundary.
/// </summary>
public sealed record PromotionScope
{
    private PromotionScope(SkuId? sku, string? category)
    {
        Sku = sku;
        Category = category;
    }

    /// <summary>The single SKU this scope targets, when SKU-scoped.</summary>
    public SkuId? Sku { get; }

    /// <summary>The single category this scope targets, when category-scoped.</summary>
    public string? Category { get; }

    public static PromotionScope ForSku(SkuId sku) =>
        sku == default
            ? throw new PricingValidationException("A SKU-scoped promotion requires a SKU identity (CC-PRC-006).")
            : new PromotionScope(sku, null);

    public static PromotionScope ForCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new PricingValidationException(
                "A category-scoped promotion requires a non-empty category identifier (CC-PRC-006).");
        }

        return new PromotionScope(null, category);
    }

    /// <summary>Whether a line with this SKU and (optional) category falls inside the scope.</summary>
    public bool Matches(SkuId sku, string? category) =>
        Sku is { } scopedSku
            ? scopedSku == sku
            : category is not null && string.Equals(Category, category, StringComparison.Ordinal);
}
