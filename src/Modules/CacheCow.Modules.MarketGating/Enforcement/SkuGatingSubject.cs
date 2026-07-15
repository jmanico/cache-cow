using CacheCow.Modules.MarketGating.Policy;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.MarketGating.Enforcement;

/// <summary>
/// A SKU as presented for gating: identity plus the veg/non-veg classification
/// supplied by the caller — catalog data is owned by the Catalog &amp;
/// Inventory context (CC-CAT-001), not by gating.
/// </summary>
public sealed record SkuGatingSubject(SkuId SkuId, SkuClassification Classification);
