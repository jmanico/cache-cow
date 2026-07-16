using CacheCow.Modules.CatalogInventory.Catalog;
using CacheCow.Modules.MarketGating.Enforcement;
using CacheCow.Modules.OrderingPayments.Submission;
using CacheCow.Modules.PricingPromotions.Pricing;
using CacheCow.Modules.PricingPromotions.Promotions;
using CacheCow.Modules.PricingPromotions.Rounding;
using CacheCow.SharedKernel;
using OrderingPromotionEvaluator = CacheCow.Modules.OrderingPayments.Submission.IPromotionEvaluator;
using PricingPromotionEvaluator = CacheCow.Modules.PricingPromotions.Promotions.IPromotionEvaluator;

namespace CacheCow.Host.Composition;

/// <summary>
/// Money-scaling rounding policy for the composition seam. UNRATIFIED: no
/// rounding mode is ratified anywhere in the canonical documents (issues
/// 002/033/034, Open Questions; <see cref="RoundingMode"/> keeps its zero
/// value unassigned for the same reason), so this type carries NO default.
/// It is populated only from explicit configuration
/// (<see cref="ConfigurationKey"/>); while unconfigured, every money path
/// that needs rounding fails closed at use — an order submission is rejected,
/// never computed with a guessed mode (CC-PRC-003). AWAITING HUMAN
/// RATIFICATION.
/// </summary>
public sealed class MoneyRoundingPolicy
{
    public const string ConfigurationKey = "CacheCow:Money:DiscountRoundingMode";

    private readonly RoundingMode? _discountRounding;

    private MoneyRoundingPolicy(RoundingMode? discountRounding)
    {
        _discountRounding = discountRounding;
    }

    /// <summary>An explicitly unconfigured policy: every use fails closed.</summary>
    public static MoneyRoundingPolicy Unconfigured { get; } = new(null);

    /// <summary>
    /// Reads the policy from configuration. Absent = unconfigured (fail closed
    /// at use); present but not an exact <see cref="RoundingMode"/> name =
    /// rejected outright (SECURITY.md, Input validation rule 1 — reject,
    /// never coerce).
    /// </summary>
    public static MoneyRoundingPolicy FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var value = configuration[ConfigurationKey];
        if (string.IsNullOrWhiteSpace(value))
        {
            return Unconfigured;
        }

        if (!Enum.TryParse<RoundingMode>(value, ignoreCase: false, out var mode) || !Enum.IsDefined(mode))
        {
            throw new InvalidOperationException(
                $"'{ConfigurationKey}' is not a recognized RoundingMode name; rejecting rather than guessing a rounding policy (CC-PRC-003).");
        }

        return new MoneyRoundingPolicy(mode);
    }

    /// <summary>The configured discount rounding mode, or a fail-closed error while the policy is unratified/unconfigured.</summary>
    public RoundingMode RequireDiscountRounding() =>
        _discountRounding ?? throw new InvalidOperationException(
            $"No money rounding policy is configured ('{ConfigurationKey}'). The rounding policy is not ratified (issues 002/033/034, Open Questions) — failing closed rather than defaulting. AWAITING HUMAN RATIFICATION.");
}

/// <summary>
/// Host-owned source of the candidate promotion records handed to the
/// Pricing &amp; Promotions evaluation engine (server data, never
/// client-claimed terms — CC-PRC-006). Promotion administration and its
/// durable store are not yet specified by any landed issue, so the shipped
/// default is <see cref="NoPromotionsConfiguredSource"/>: zero candidates,
/// zero discounts (the fail-closed direction — a missing promotion source can
/// never grant money off).
/// </summary>
public interface IActivePromotionSource
{
    /// <summary>All candidate promotion records for the market; window filtering is the evaluator's (CC-PRC-006).</summary>
    IReadOnlyList<Promotion> CandidatesFor(Market market);
}

/// <summary>No promotion data exists yet: no promotion ever applies (fail closed).</summary>
public sealed class NoPromotionsConfiguredSource : IActivePromotionSource
{
    /// <inheritdoc />
    public IReadOnlyList<Promotion> CandidatesFor(Market market) => [];
}

/// <summary>
/// OrderingPayments <see cref="ICanonicalPriceSource"/> → PricingPromotions
/// <see cref="PriceList"/> (ARCHITECTURE.md, Dependency rule 2: only the
/// Ordering service computes money, from Pricing as canonical source;
/// CC-PRC-001/005). Per the port's contract the gating consultation is
/// adapted upstream by the host, so this adapter consults the REAL Market
/// &amp; Gating service first (Dependency rule 1): a SKU gated out of the
/// transacting market is not priceable there — consumer ordering has exactly
/// the same CC-MKT-003 posture as every other surface. A SKU with no price
/// row in the market is likewise unpriceable (no FX conversion, no fallback).
/// </summary>
internal sealed class PriceListCanonicalPriceSource : ICanonicalPriceSource
{
    private readonly PriceList _priceList;
    private readonly ISkuCatalog _catalog;
    private readonly IMarketGatingService _gating;

    public PriceListCanonicalPriceSource(PriceList priceList, ISkuCatalog catalog, IMarketGatingService gating)
    {
        ArgumentNullException.ThrowIfNull(priceList);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(gating);
        _priceList = priceList;
        _catalog = catalog;
        _gating = gating;
    }

    /// <inheritdoc />
    public bool TryGetUnitPrice(SkuId sku, Market market, out Money unitPrice)
    {
        unitPrice = default;

        // Ordering serves the consumer product surface; gate on it
        // (CC-MKT-003 — a SKU you cannot be shown you cannot order).
        if (!SkuGating.IsPermitted(_gating, _catalog, market, sku, ResponseSurface.ProductDetail))
        {
            return false;
        }

        var lookup = _priceList.Lookup(sku, market);
        if (!lookup.IsPriced)
        {
            return false;
        }

        unitPrice = lookup.Price.UnitPrice;
        return true;
    }
}

/// <summary>
/// OrderingPayments <see cref="IPromotionEvaluator"/> → the PricingPromotions
/// evaluation engine (CC-PRC-006: the order service is final authority;
/// evaluation happens at submission time against the authoritative server
/// clock — the engine trusts only its injected <see cref="TimeProvider"/>,
/// the same host singleton that stamps the submission, so the port's
/// submittedAt and the engine's clock agree by construction).
///
/// Required-but-unratified inputs fail closed rather than defaulting:
/// the discount <see cref="RoundingMode"/> comes only from
/// <see cref="MoneyRoundingPolicy"/> (throws while unconfigured), and the
/// market-timezone mapping stays the module's fail-closed
/// UnconfiguredMarketTimeZoneProvider until a human ratifies it. With zero
/// candidate promotions neither is consulted and the discount is exactly
/// zero — no promotion data can never mean a granted discount.
/// </summary>
internal sealed class PricingPromotionEvaluatorAdapter : OrderingPromotionEvaluator
{
    private readonly PricingPromotionEvaluator _engine;
    private readonly PriceList _priceList;
    private readonly ISkuCatalog _catalog;
    private readonly IActivePromotionSource _promotions;
    private readonly MoneyRoundingPolicy _rounding;

    public PricingPromotionEvaluatorAdapter(
        PricingPromotionEvaluator engine,
        PriceList priceList,
        ISkuCatalog catalog,
        IActivePromotionSource promotions,
        MoneyRoundingPolicy rounding)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(priceList);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(promotions);
        ArgumentNullException.ThrowIfNull(rounding);
        _engine = engine;
        _priceList = priceList;
        _catalog = catalog;
        _promotions = promotions;
        _rounding = rounding;
    }

    /// <inheritdoc />
    public Money EvaluateDiscount(SkuId sku, Market market, int quantity, Money lineSubtotal, DateTimeOffset submittedAt)
    {
        var candidates = _promotions.CandidatesFor(market);
        if (candidates.Count == 0)
        {
            // No promotion data exists: zero discount, and neither the
            // unratified rounding mode nor the unratified timezone mapping is
            // needed to say so (fail-closed direction: nothing is granted).
            return Money.FromMinorUnits(0, lineSubtotal.Currency);
        }

        // Both are unratified configuration; unconfigured state surfaces as a
        // fail-closed error that rejects the submission (port contract: any
        // failure fails the submission closed).
        var rounding = _rounding.RequireDiscountRounding();

        // Recompute the line from canonical pricing (CC-PRC-005) and require
        // the caller's subtotal to match it exactly — a divergence means a
        // non-canonical amount reached the money path (fail closed).
        var lookup = _priceList.Lookup(sku, market);
        var unitPrice = lookup.Price.UnitPrice; // throws PriceUnavailableException on a miss (fail closed)
        if (unitPrice.MultiplyBy(quantity) != lineSubtotal)
        {
            throw new InvalidOperationException(
                $"Line subtotal for SKU '{sku.Value}' does not equal the canonical unit price × quantity in market {market.Code}; failing closed (CC-PRC-005).");
        }

        var category = _catalog.TryGet(sku, out var catalogSku) ? catalogSku.CutCategory.Value : null;

        var result = _engine.Evaluate(new PromotionEvaluationRequest(
            market,
            [new PromotionLine(sku, unitPrice, quantity, category)],
            candidates,
            rounding));

        return result.Lines[0].Discount;
    }
}
