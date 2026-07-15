using CacheCow.SharedKernel;

namespace CacheCow.Modules.ContentLocalization.Resources;

/// <summary>
/// The closed set of seven launch locales for string resources (CC-I18N-001):
/// en-US, es-ES, es-MX, de-DE, ja-JP, en-IN, hi-IN. Every resource set MUST
/// cover exactly this set with key parity (CC-I18N-002); a locale outside it
/// is rejected, never accepted provisionally (SECURITY.md, Input validation
/// rule 1). The transacting-locale allowlist used for gating and cache keys
/// belongs to the Market &amp; Gating Policy context, not here.
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
}
