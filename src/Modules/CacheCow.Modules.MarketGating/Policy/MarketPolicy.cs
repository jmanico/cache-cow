using CacheCow.SharedKernel;

namespace CacheCow.Modules.MarketGating.Policy;

/// <summary>
/// The immutable per-market gating policy record (CC-MKT-001, CC-MKT-006):
/// catalog gating, content-experience availability, currency, tax-display
/// convention, veg-marking scheme, and legal content set — encoded as data,
/// never as conditionals scattered through consuming modules. The single
/// authoring point is <see cref="LaunchMarketPolicies"/>.
/// </summary>
public sealed record MarketPolicy
{
    public MarketPolicy(
        Market market,
        bool nonVegSkusPermitted,
        Currency currency,
        TaxDisplayConvention taxDisplay,
        bool displaysUnitPricePerKilogram,
        VegetarianMarking vegMarking,
        bool nonVegMarkProhibited,
        ContentPlacement meetOurCutsPlacement,
        ContentPlacement meetOurCowsPlacement,
        IReadOnlySet<LegalDocument> legalContentSet)
    {
        ArgumentNullException.ThrowIfNull(currency);
        ArgumentNullException.ThrowIfNull(legalContentSet);

        Market = market;
        NonVegSkusPermitted = nonVegSkusPermitted;
        Currency = currency;
        TaxDisplay = taxDisplay;
        DisplaysUnitPricePerKilogram = displaysUnitPricePerKilogram;
        VegMarking = vegMarking;
        NonVegMarkProhibited = nonVegMarkProhibited;
        MeetOurCutsPlacement = meetOurCutsPlacement;
        MeetOurCowsPlacement = meetOurCowsPlacement;
        LegalContentSet = legalContentSet;
    }

    /// <summary>The launch market this policy governs (CC-MKT-001).</summary>
    public Market Market { get; }

    /// <summary>
    /// Whether non-veg SKUs may appear in any response for this market.
    /// False for IN (veg-only catalog, CC-MKT-003); true for US/ES/DE
    /// (full catalog, CC-MKT-007). JP and MX are encoded as non-restricted —
    /// no requirement gates their catalog — but CC-MKT-007 names only
    /// US/ES/DE for the full catalog, so their breadth is policy data,
    /// not a requirement assertion (issue 025, Open Questions).
    /// </summary>
    public bool NonVegSkusPermitted { get; }

    /// <summary>The market's single consumer currency (CC-PRC-001).</summary>
    public Currency Currency { get; }

    /// <summary>Price/tax display convention (CC-PRC-002).</summary>
    public TaxDisplayConvention TaxDisplay { get; }

    /// <summary>DE: unit price per kilogram alongside every price (Preisangabenverordnung, CC-PRC-002).</summary>
    public bool DisplaysUnitPricePerKilogram { get; }

    /// <summary>Which vegetarian marking scheme product presentations use (CC-CNT-006; DESIGN.md §3.3).</summary>
    public VegetarianMarking VegMarking { get; }

    /// <summary>IN: the FSSAI non-veg mark MUST NOT appear anywhere (CC-CNT-006).</summary>
    public bool NonVegMarkProhibited { get; }

    /// <summary>"Meet our Cuts" placement: NotAvailable in IN (CC-MKT-005).</summary>
    public ContentPlacement MeetOurCutsPlacement { get; }

    /// <summary>"Meet our Cows" placement: primary navigation in IN, Our Story elsewhere (CC-MKT-005).</summary>
    public ContentPlacement MeetOurCowsPlacement { get; }

    /// <summary>The legal documents this market must serve (CC-CNT-005).</summary>
    public IReadOnlySet<LegalDocument> LegalContentSet { get; }

    /// <summary>
    /// Placement lookup by experience. Unknown experiences resolve to
    /// <see cref="ContentPlacement.NotAvailable"/> (fail closed,
    /// SECURITY.md Logging rule 2).
    /// </summary>
    public ContentPlacement PlacementOf(ContentExperience experience) => experience switch
    {
        ContentExperience.MeetOurCuts => MeetOurCutsPlacement,
        ContentExperience.MeetOurCows => MeetOurCowsPlacement,
        _ => ContentPlacement.NotAvailable,
    };
}
