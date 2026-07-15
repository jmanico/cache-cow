using System.Globalization;
using CacheCow.Modules.CatalogInventory.Catalog;

namespace CacheCow.Modules.CatalogInventory.Search;

/// <summary>
/// In-memory <see cref="ICatalogSearchService"/> over the domain model
/// (issue 031). Matching is a culture-correct normalized substring match on
/// the localized name in the query locale (ja-JP queries match Japanese
/// names, hi-IN queries match Devanagari names — CC-CAT-005), ignoring case,
/// kana type, and character width via ICU collation.
/// Interim quality note, flagged not resolved: PostgreSQL's built-in FTS has
/// no ja-JP tokenizer and limited hi-IN handling; the mechanism that makes the
/// confirmed engine satisfy CC-CAT-005 (extension, n-gram, or normalization
/// strategy) is an open question on issue 031 awaiting a human decision
/// consistent with SECURITY.md Dependency Rules. This substring matcher is the
/// behavioral reference the FTS adapter must meet, not the decision itself.
/// </summary>
public sealed class InMemoryCatalogSearchService : ICatalogSearchService
{
    private const CompareOptions MatchOptions =
        CompareOptions.IgnoreCase | CompareOptions.IgnoreKanaType | CompareOptions.IgnoreWidth;

    private readonly ISkuCatalog _catalog;

    public InMemoryCatalogSearchService(ISkuCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    public IReadOnlyList<Sku> Search(CatalogSearchQuery query, Func<ProductClassification, bool> classificationPermitted)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(classificationPermitted);

        var results = new List<Sku>();
        foreach (var sku in _catalog.All())
        {
            // Market scope: only SKUs available in the requester's transacting
            // market surface (CC-CAT-005; issue 031 AC-04). Fail closed on a
            // missing flag.
            if (!sku.IsAvailableIn(query.TransactingMarket))
            {
                continue;
            }

            // Gating composition: the caller's predicate is consulted for every
            // candidate; a predicate failure throws out of the loop — no result
            // set is ever produced ungated (issue 031, Failure Behavior).
            if (!classificationPermitted(sku.Classification))
            {
                continue;
            }

            // Single-toggle vegetarian filter on the structured classification
            // field, in every market (CC-CAT-006, CC-MKT-007).
            if (query.VegetarianOnly && sku.Classification != ProductClassification.Vegetarian)
            {
                continue;
            }

            if (MatchesName(sku, query))
            {
                results.Add(sku);
            }
        }

        // Deterministic ordering; relevance ranking is unspecified in
        // CC-CAT-005 (issue 031, Open Questions).
        results.Sort(static (left, right) => string.CompareOrdinal(left.Id.Value, right.Id.Value));
        return results;
    }

    private static bool MatchesName(Sku sku, CatalogSearchQuery query)
    {
        if (query.Text.Length == 0)
        {
            return true;
        }

        // Per-locale matching: the name in the query locale only. Which SKU
        // fields beyond the localized name are in search scope is unspecified
        // in CC-CAT-005 (issue 031, Open Questions) — name-only until decided.
        if (!sku.Name.TryGet(query.QueryLocale, out var localizedName))
        {
            return false;
        }

        return CultureInfo.InvariantCulture.CompareInfo
            .IndexOf(localizedName, query.Text, MatchOptions) >= 0;
    }
}
