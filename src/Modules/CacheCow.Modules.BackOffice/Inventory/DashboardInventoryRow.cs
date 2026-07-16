using CacheCow.Modules.BackOffice.Dashboard;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.BackOffice.Inventory;

/// <summary>
/// The three user-facing stock states of CC-CAT-003, as DERIVED BY the Catalog
/// &amp; Inventory context and reported to the dashboard. This module never
/// computes them: availability derives from the cold store(s) serving a market
/// (CC-CAT-002) using catalog rules this context does not own, and
/// recomputing them here would be a second source of truth (ARCHITECTURE.md,
/// Dependency rule 1 — nothing implements its own market conditionals; issue
/// 084, AC-07).
///
/// The dashboard's own presentation of these states is DESIGN.md 5.2's
/// cache vocabulary (CACHE HIT / WARMING / CACHE MISS), which is the client's
/// concern, not this enum's.
/// </summary>
public enum DashboardStockState
{
    /// <summary>In stock — ships from the regional cold store.</summary>
    InStock = 0,

    /// <summary>Restocking; preorder permitted.</summary>
    Restocking = 1,

    /// <summary>Unavailable in region.</summary>
    UnavailableInRegion = 2,
}

/// <summary>
/// One per-SKU, per-cold-store inventory row for the dashboard's "inventory by
/// cold store" module (issue 084; CC-DSH-003, CC-CAT-002).
///
/// Classification is Internal: operational inventory only, NO PII (issue 084,
/// Data Classification). No money appears here either — this module displays
/// stock, not prices.
/// </summary>
public sealed record DashboardInventoryRow
{
    /// <summary>Maximum length of a cold-store identifier.</summary>
    public const int MaxColdStoreIdLength = 64;

    /// <summary>Basis points in a whole unit: 10000 bp = 100%.</summary>
    public const int BasisPointsScale = 10_000;

    private DashboardInventoryRow(
        string coldStoreId,
        Market market,
        SkuId sku,
        long quantityOnHand,
        DashboardStockState stockState,
        int? serviceLevelBasisPoints)
    {
        ColdStoreId = coldStoreId;
        Market = market;
        Sku = sku;
        QuantityOnHand = quantityOnHand;
        StockState = stockState;
        ServiceLevelBasisPoints = serviceLevelBasisPoints;
    }

    /// <summary>The regional cold store this row counts (REQUIREMENTS.md §2, "Regional cold store").</summary>
    public string ColdStoreId { get; }

    /// <summary>The market this cold store serves for this row (CC-MKT-001, CC-CAT-002).</summary>
    public Market Market { get; }

    /// <summary>The SKU counted (CC-CAT-001).</summary>
    public SkuId Sku { get; }

    /// <summary>Units held in this cold store. A count, never money; never negative.</summary>
    public long QuantityOnHand { get; }

    /// <summary>The CC-CAT-003 state the Catalog &amp; Inventory context derived for this SKU in this market.</summary>
    public DashboardStockState StockState { get; }

    /// <summary>
    /// The per-region per-SKU stock service level of CC-DSH-006 — the "cache
    /// hit rate" — in BASIS POINTS (0..10000, so 9 950 = 99.50%), or null when
    /// the supplying context has no figure for this row (e.g. too little
    /// history).
    ///
    /// INTEGER, NOT A DOUBLE, deliberately. CC-PRC-003 bans binary floating
    /// point for money; this is a rate rather than money, but a float here
    /// would still be the wrong tool — it makes an exact, bounded ratio
    /// inexact, and non-associative summation would render two equal service
    /// levels as unequal in a grid built for scanning. Basis points give the
    /// full ratified reporting precision (the CC-NFR-001 availability targets
    /// are quoted to two decimals: 99.9%, 99.5%) with exact equality and exact
    /// ordering.
    ///
    /// SUPPLIED, NEVER COMPUTED HERE. Issue 084 places the CC-DSH-006 metric
    /// with the sales-analytics module (issue 083) and explicitly out of scope
    /// for this one. This field therefore carries a figure the owning context
    /// hands over for display alongside the stock it describes; this module
    /// defines no metric, no window, and no formula. See the module's open
    /// questions.
    /// </summary>
    public int? ServiceLevelBasisPoints { get; }

    /// <summary>Creates a validated inventory row.</summary>
    /// <exception cref="DashboardValidationException">Any field is invalid.</exception>
    public static DashboardInventoryRow Create(
        string coldStoreId,
        Market market,
        SkuId sku,
        long quantityOnHand,
        DashboardStockState stockState,
        int? serviceLevelBasisPoints = null)
    {
        ValidateColdStoreId(coldStoreId);

        if (market == default)
        {
            throw new DashboardValidationException(
                "An inventory row requires an initialized launch market (CC-MKT-001).");
        }

        if (sku == default)
        {
            throw new DashboardValidationException(
                "An inventory row requires an initialized SKU identity (CC-CAT-001).");
        }

        if (quantityOnHand < 0)
        {
            throw new DashboardValidationException(
                "An inventory row's quantity on hand must not be negative (CC-CAT-002).");
        }

        if (!Enum.IsDefined(stockState))
        {
            throw new DashboardValidationException(
                $"Stock state {(int)stockState} is outside the CC-CAT-003 closed set of three states; rejected (SECURITY.md, Input validation rule 1).");
        }

        if (serviceLevelBasisPoints is { } basisPoints && (basisPoints < 0 || basisPoints > BasisPointsScale))
        {
            throw new DashboardValidationException(
                $"A service level must be between 0 and {BasisPointsScale} basis points (CC-DSH-006); rejected, never clamped (SECURITY.md, Input validation rule 1).");
        }

        return new DashboardInventoryRow(coldStoreId, market, sku, quantityOnHand, stockState, serviceLevelBasisPoints);
    }

    /// <summary>
    /// Validates a cold-store identifier: required, bounded, and free of
    /// control characters (log-injection vector — SECURITY.md, Logging
    /// rule 5). Rejected, never trimmed into acceptance.
    /// </summary>
    public static void ValidateColdStoreId(string coldStoreId)
    {
        if (string.IsNullOrWhiteSpace(coldStoreId) || coldStoreId.Length > MaxColdStoreIdLength)
        {
            throw new DashboardValidationException(
                $"A cold-store identifier is required and at most {MaxColdStoreIdLength} characters (SECURITY.md, Input validation rule 1).");
        }

        foreach (var character in coldStoreId)
        {
            if (char.IsControl(character))
            {
                throw new DashboardValidationException(
                    "A cold-store identifier must not contain control characters (SECURITY.md, Logging rule 5).");
            }
        }
    }
}
