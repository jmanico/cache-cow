using CacheCow.Modules.CatalogInventory.Catalog;
using CacheCow.Modules.MarketGating.Enforcement;
using CacheCow.Modules.MarketGating.Policy;
using CacheCow.Modules.MarketGating.Resolution;
using CacheCow.Modules.WholesaleB2B.Gating;
using CacheCow.SharedKernel;

namespace CacheCow.Host.Composition;

/// <summary>
/// Cross-context gating glue owned by the host (ARCHITECTURE.md, Dependency
/// rule 1: the Market &amp; Gating Policy service is the single enforcement
/// point; nothing implements its own market conditionals).
/// </summary>
internal static class SkuGating
{
    /// <summary>
    /// Evaluates one SKU against the REAL Market &amp; Gating service for a
    /// server-side transacting market. The SKU's veg/non-veg classification is
    /// resolved from the Catalog context's structured data (CC-CAT-001);
    /// a SKU the catalog cannot classify is denied (fail closed, SECURITY.md
    /// Logging rule 2). Exceptions propagate — every caller treats a thrown
    /// gating fault as a denial per its own port contract.
    /// </summary>
    internal static bool IsPermitted(
        IMarketGatingService gating,
        ISkuCatalog catalog,
        Market market,
        SkuId skuId,
        ResponseSurface surface)
    {
        if (!catalog.TryGet(skuId, out var sku))
        {
            // Unknown to the catalog = unclassifiable = denied (fail closed).
            return false;
        }

        var classification = sku.Classification switch
        {
            ProductClassification.Vegetarian => SkuClassification.Vegetarian,
            _ => SkuClassification.NonVegetarian, // most restrictive (fail closed)
        };

        var context = new TransactingContext(market, CarrierLocaleFor(market));
        return gating
            .EvaluateSku(context, new SkuGatingSubject(skuId, classification), surface)
            .IsAllowed;
    }

    /// <summary>
    /// <see cref="TransactingContext"/> is valid only with a launch locale,
    /// but SKU gating decisions are a pure function of market, classification,
    /// and surface — the locale never influences the outcome
    /// (<see cref="MarketGatingService"/>; CC-SEC-012 keys gating exclusively
    /// off the transacting market). This helper supplies a deterministic
    /// structural carrier locale (the ordinal-first launch locale whose region
    /// subtag is the market) purely to satisfy the type's invariant. It does
    /// NOT resolve the open IN primary-locale decision (ARCHITECTURE.md
    /// discipline) — that decision concerns user-facing fallback language,
    /// which this value never touches.
    /// </summary>
    internal static Locale CarrierLocaleFor(Market market)
    {
        foreach (var locale in LaunchLocales.All.OrderBy(l => l.Tag, StringComparer.Ordinal))
        {
            if (locale.Tag.EndsWith("-" + market.Code, StringComparison.Ordinal))
            {
                return locale;
            }
        }

        // Unreachable for the six launch markets; fail closed if it ever isn't.
        throw new InvalidOperationException(
            $"No launch locale carries region '{market.Code}' (CC-I18N-001); cannot construct a transacting context.");
    }
}

/// <summary>
/// WholesaleB2B <see cref="IB2BGatingCheck"/> → MarketGating
/// <see cref="IMarketGatingService"/> (CC-API-007: market gating applies to
/// the API identically to the storefront — a partner authorized for the IN
/// market must not be able to see or order a non-veg SKU through any
/// endpoint). Evaluated on the <see cref="ResponseSurface.B2BApi"/> surface;
/// gated, unknown, and unclassifiable SKUs are all
/// <see cref="B2BGatingDecision.Denied"/>, and the B2B endpoints already treat
/// any thrown exception as a denial (fail closed).
/// </summary>
internal sealed class B2BMarketGatingCheckAdapter : IB2BGatingCheck
{
    private readonly IMarketGatingService _gating;
    private readonly ISkuCatalog _catalog;

    public B2BMarketGatingCheckAdapter(IMarketGatingService gating, ISkuCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(gating);
        ArgumentNullException.ThrowIfNull(catalog);
        _gating = gating;
        _catalog = catalog;
    }

    /// <inheritdoc />
    public B2BGatingDecision EvaluateSku(Market market, SkuId sku) =>
        SkuGating.IsPermitted(_gating, _catalog, market, sku, ResponseSurface.B2BApi)
            ? B2BGatingDecision.Permitted
            : B2BGatingDecision.Denied;
}
