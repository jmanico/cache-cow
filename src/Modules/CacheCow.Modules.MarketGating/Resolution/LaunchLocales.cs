using CacheCow.SharedKernel;

namespace CacheCow.Modules.MarketGating.Resolution;

/// <summary>
/// The closed set of seven launch locales (CC-I18N-001): en-US, es-ES, es-MX,
/// de-DE, ja-JP, en-IN, hi-IN. This is the transacting-locale allowlist used
/// for gating state and cache keys (CC-MKT-009); UI string resources belong to
/// the Content &amp; Localization context. Client-supplied locale identifiers
/// outside this set are rejected, never normalized into acceptance
/// (SECURITY.md, Input validation rule 1).
/// </summary>
public static class LaunchLocales
{
    public static IReadOnlyList<Locale> All { get; } =
    [
        Locale.Parse("en-US"),
        Locale.Parse("es-ES"),
        Locale.Parse("es-MX"),
        Locale.Parse("de-DE"),
        Locale.Parse("ja-JP"),
        Locale.Parse("en-IN"),
        Locale.Parse("hi-IN"),
    ];

    public static bool Contains(Locale locale) => All.Contains(locale);

    /// <summary>
    /// Parses a client-supplied locale tag against the closed launch set.
    /// Well-formed BCP 47 tags outside the set (e.g. fr-FR) are rejected too —
    /// the transacting locale is a closed set, not merely a valid tag.
    /// </summary>
    public static bool TryParse(string? tag, out Locale locale)
    {
        if (Locale.TryParse(tag, out var candidate) && All.Contains(candidate))
        {
            locale = candidate;
            return true;
        }

        locale = default;
        return false;
    }
}
