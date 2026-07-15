using CacheCow.Modules.MarketGating.Policy;
using CacheCow.Modules.MarketGating.Resolution;

namespace CacheCow.Modules.MarketGating.Enforcement;

/// <summary>
/// The single server-side enforcement point every other surface consults —
/// storefront rendering, search, recommendations, the B2B API, and
/// sitemap/feed/structured-data generation (CC-MKT-003, CC-MKT-006;
/// ARCHITECTURE.md, Dependency rule 1: "nothing may implement its own market
/// conditionals ... Clients never gate"). The only accepted gating input is
/// the server-side <see cref="TransactingContext"/> (CC-SEC-012); every
/// unknown or invalid input denies (fail closed, SECURITY.md Logging rule 2).
/// </summary>
public interface IMarketGatingService
{
    /// <summary>
    /// Gates a single SKU for a market and declared response surface. Non-veg
    /// SKUs are excluded from every surface of a veg-only market (CC-MKT-003);
    /// exclusions present as 404 (CC-MKT-004).
    /// </summary>
    GatingDecision EvaluateSku(TransactingContext context, SkuGatingSubject subject, ResponseSurface surface);

    /// <summary>
    /// Gates a content experience: "Meet our Cuts" is unreachable in IN and
    /// presents as 404 there (CC-MKT-005; CC-MKT-004 semantics).
    /// </summary>
    GatingDecision EvaluateContentExperience(TransactingContext context, ContentExperience experience);

    /// <summary>
    /// Navigation placement for a content experience per market policy
    /// (CC-MKT-005: Cows in primary navigation in IN, under Our Story
    /// elsewhere). Unknown inputs resolve to
    /// <see cref="ContentPlacement.NotAvailable"/> (fail closed).
    /// </summary>
    ContentPlacement PlacementOf(TransactingContext context, ContentExperience experience);

    /// <summary>
    /// Server-side bulk exclusion for listing-shaped surfaces: everything the
    /// classification selector marks non-veg (or cannot classify — fail
    /// closed) is removed before serialization, so no gated item can reach a
    /// response body, hydration payload, sitemap, or feed (CC-MKT-003). The
    /// result is wrapped as <see cref="Gated{T}"/>, proof it was gated for
    /// exactly this context (issue 028).
    /// </summary>
    Gated<IReadOnlyList<T>> FilterSkus<T>(
        TransactingContext context,
        IEnumerable<T> items,
        Func<T, SkuClassification> classification,
        ResponseSurface surface);
}
