using CacheCow.SharedKernel;
using System.Diagnostics.CodeAnalysis;

namespace CacheCow.Modules.MarketGating.Policy;

/// <summary>
/// The declarative per-market policy table for exactly the six launch markets
/// (CC-MKT-001) — the policy-as-data substrate of the Market &amp; Gating
/// Policy bounded context (CC-MKT-006; ARCHITECTURE.md, "Server bounded
/// contexts" 1). All gating rules live here as reviewable data; no consuming
/// module carries a market conditional (ARCHITECTURE.md, Dependency rule 1).
/// Lookups for anything outside the table fail closed (SECURITY.md, Logging
/// rule 2): there is no default, ungated policy.
/// The durable storage/change-management mechanism for this configuration
/// (repo-versioned vs. dashboard-administered) is an open question in issue
/// 023; this in-code table is the schema-checked declarative source until a
/// human decides.
/// </summary>
public static class LaunchMarketPolicies
{
    private static readonly IReadOnlySet<LegalDocument> BaseLegalSet = new HashSet<LegalDocument>
    {
        LegalDocument.PrivacyPolicy,
        LegalDocument.Terms,
        LegalDocument.ShippingAndReturns,
    };

    private static readonly IReadOnlySet<LegalDocument> GermanyLegalSet = new HashSet<LegalDocument>
    {
        LegalDocument.PrivacyPolicy,
        LegalDocument.Terms,
        LegalDocument.ShippingAndReturns,
        LegalDocument.Impressum,
        LegalDocument.Widerrufsbelehrung,
    };

    private static readonly Dictionary<Market, MarketPolicy> ByMarket = BuildTable();

    /// <summary>Every launch-market policy, exactly one per market (CC-MKT-001).</summary>
    public static IReadOnlyCollection<MarketPolicy> All => ByMarket.Values;

    /// <summary>
    /// Fail-closed policy lookup: false for any market not in the launch table
    /// (including an uninitialized <see cref="Market"/> value). Callers treat
    /// false as most-restrictive/denied, never as "ungated" (issue 023 AC-05).
    /// </summary>
    public static bool TryGet(Market market, [NotNullWhen(true)] out MarketPolicy? policy) =>
        ByMarket.TryGetValue(market, out policy);

    private static Dictionary<Market, MarketPolicy> BuildTable()
    {
        // One row per launch market. This is the reviewable gating table:
        // catalog gating (CC-MKT-003/007), Cuts/Cows placement (CC-MKT-005),
        // currency (CC-PRC-001), tax display (CC-PRC-002), veg marking
        // (CC-CNT-006), legal content set (CC-CNT-005).
        MarketPolicy[] rows =
        [
            new(
                market: Market.US,
                nonVegSkusPermitted: true,               // full catalog (CC-MKT-007)
                currency: Currency.Usd,                  // CC-PRC-001
                taxDisplay: TaxDisplayConvention.TaxExclusiveEstimatedAtCheckout, // CC-PRC-002
                displaysUnitPricePerKilogram: false,
                vegMarking: VegetarianMarking.LeafDotBadge,
                nonVegMarkProhibited: false,
                meetOurCutsPlacement: ContentPlacement.StandardPage,
                meetOurCowsPlacement: ContentPlacement.UnderOurStory, // CC-MKT-005
                legalContentSet: BaseLegalSet),
            new(
                market: Market.ES,
                nonVegSkusPermitted: true,               // full catalog (CC-MKT-007)
                currency: Currency.Eur,
                taxDisplay: TaxDisplayConvention.TaxInclusive,
                displaysUnitPricePerKilogram: false,
                vegMarking: VegetarianMarking.LeafDotBadge,
                nonVegMarkProhibited: false,
                meetOurCutsPlacement: ContentPlacement.StandardPage,
                meetOurCowsPlacement: ContentPlacement.UnderOurStory,
                legalContentSet: BaseLegalSet),
            new(
                market: Market.MX,
                // Not veg-restricted: no requirement gates the MX catalog, but
                // CC-MKT-007 names only US/ES/DE as full-catalog — breadth here
                // is policy data, not a requirement (issue 025, Open Questions).
                nonVegSkusPermitted: true,
                currency: Currency.Mxn,
                taxDisplay: TaxDisplayConvention.TaxInclusive, // MX IVA-inclusive (CC-PRC-002)
                displaysUnitPricePerKilogram: false,
                vegMarking: VegetarianMarking.LeafDotBadge,
                nonVegMarkProhibited: false,
                meetOurCutsPlacement: ContentPlacement.StandardPage,
                meetOurCowsPlacement: ContentPlacement.UnderOurStory,
                legalContentSet: BaseLegalSet),
            new(
                market: Market.DE,
                nonVegSkusPermitted: true,               // full catalog (CC-MKT-007)
                currency: Currency.Eur,
                taxDisplay: TaxDisplayConvention.TaxInclusive,
                displaysUnitPricePerKilogram: true,      // Preisangabenverordnung (CC-PRC-002)
                vegMarking: VegetarianMarking.LeafDotBadge,
                nonVegMarkProhibited: false,
                meetOurCutsPlacement: ContentPlacement.StandardPage,
                meetOurCowsPlacement: ContentPlacement.UnderOurStory,
                legalContentSet: GermanyLegalSet),       // + Impressum, Widerrufsbelehrung (CC-CNT-005)
            new(
                market: Market.JP,
                // Not veg-restricted: DESIGN.md §8.5 says full catalog, but
                // CC-MKT-007 names only US/ES/DE — REQUIREMENTS.md takes
                // precedence, so JP breadth beyond "not veg-restricted" is
                // policy data, not a requirement (issue 025, Open Questions).
                nonVegSkusPermitted: true,
                currency: Currency.Jpy,
                taxDisplay: TaxDisplayConvention.TaxInclusive,
                displaysUnitPricePerKilogram: false,
                vegMarking: VegetarianMarking.LeafDotBadge,
                nonVegMarkProhibited: false,
                meetOurCutsPlacement: ContentPlacement.StandardPage,
                meetOurCowsPlacement: ContentPlacement.UnderOurStory,
                legalContentSet: BaseLegalSet),
            new(
                market: Market.IN,
                nonVegSkusPermitted: false,              // vegetarian-only catalog (CC-MKT-003)
                currency: Currency.Inr,
                taxDisplay: TaxDisplayConvention.TaxInclusive, // GST line on invoice (CC-PRC-002)
                displaysUnitPricePerKilogram: false,
                vegMarking: VegetarianMarking.FssaiRegulatoryMark, // CC-CNT-006
                nonVegMarkProhibited: true,              // CC-CNT-006
                meetOurCutsPlacement: ContentPlacement.NotAvailable,     // CC-MKT-005
                meetOurCowsPlacement: ContentPlacement.PrimaryNavigation, // CC-MKT-005
                legalContentSet: BaseLegalSet),
        ];

        // Schema-level closure check (issue 023 AC-01): exactly one policy per
        // launch market, no extras, no gaps. A malformed table refuses to load
        // rather than serving a partial policy set (fail closed).
        var table = rows.ToDictionary(r => r.Market);
        if (table.Count != Market.All.Count || Market.All.Any(m => !table.ContainsKey(m)))
        {
            throw new InvalidOperationException(
                "Launch market policy table must cover exactly the six launch markets (CC-MKT-001).");
        }

        return table;
    }
}
