using CacheCow.SharedKernel;

namespace CacheCow.Modules.CatalogInventory.Search;

/// <summary>
/// A validated catalog search request (CC-CAT-005). The transacting market and
/// query locale are server-side resolved state (issue 024) — never
/// `Accept-Language`, geolocation, or any client hint (CC-SEC-012; SECURITY.md,
/// Authentication rule 10). Query text is attacker-controlled input: oversized
/// text is rejected, never truncated into acceptance (SECURITY.md, Input
/// validation rule 1; HTTP boundary rule 7). Empty text is a valid
/// browse-everything query.
/// </summary>
public sealed class CatalogSearchQuery
{
    /// <summary>Upper bound on query text length (SECURITY.md, HTTP boundary rule 7).</summary>
    public const int MaxTextLength = 256;

    private CatalogSearchQuery(Market transactingMarket, Locale queryLocale, string text, bool vegetarianOnly)
    {
        TransactingMarket = transactingMarket;
        QueryLocale = queryLocale;
        Text = text;
        VegetarianOnly = vegetarianOnly;
    }

    public Market TransactingMarket { get; }

    public Locale QueryLocale { get; }

    public string Text { get; }

    /// <summary>The single-toggle vegetarian filter, available in all markets (CC-CAT-006).</summary>
    public bool VegetarianOnly { get; }

    public static CatalogSearchQuery Create(
        Market transactingMarket,
        Locale queryLocale,
        string text,
        bool vegetarianOnly = false)
    {
        if (transactingMarket == default)
        {
            throw new ArgumentException(
                "Search requires the server-side transacting market (CC-CAT-005, CC-SEC-012).",
                nameof(transactingMarket));
        }

        if (queryLocale == default)
        {
            throw new ArgumentException(
                "Search requires an initialized query locale (CC-CAT-005).", nameof(queryLocale));
        }

        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > MaxTextLength)
        {
            throw new ArgumentException(
                $"Query text exceeds {MaxTextLength} characters and is rejected, not truncated (SECURITY.md, HTTP boundary rule 7).",
                nameof(text));
        }

        return new CatalogSearchQuery(transactingMarket, queryLocale, text, vegetarianOnly);
    }
}
