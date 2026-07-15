using System.Diagnostics.CodeAnalysis;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.CatalogInventory.Catalog;

/// <summary>
/// A user-facing text field keyed by BCP 47 locale (REQUIREMENTS.md §2;
/// CC-CAT-001 "name (localized)"). Construction rejects empty maps,
/// uninitialized locale keys, and blank values — reject, never sanitize
/// (SECURITY.md, Input validation rule 1). Lookup is exact-locale only and
/// fail-closed (no match, no value): fallback policy is a Content &amp;
/// Localization concern, not encoded here (issue 029, Open Questions — which
/// fields carry per-locale variants and how fallback works is unspecified).
/// </summary>
public sealed class LocalizedText
{
    private readonly Dictionary<Locale, string> _values;

    private LocalizedText(Dictionary<Locale, string> values)
    {
        _values = values;
    }

    public IReadOnlyCollection<Locale> Locales => _values.Keys;

    public static LocalizedText Create(IReadOnlyDictionary<Locale, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new ArgumentException(
                "Localized text requires at least one locale entry (CC-CAT-001).", nameof(values));
        }

        var copy = new Dictionary<Locale, string>(values.Count);
        foreach (var (locale, text) in values)
        {
            if (locale == default)
            {
                throw new ArgumentException(
                    "Localized text keys must be initialized locales (REQUIREMENTS.md §2).", nameof(values));
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException(
                    $"Localized text for '{locale}' must be non-empty; blank values are rejected, not defaulted (SECURITY.md, Input validation rule 1).",
                    nameof(values));
            }

            copy[locale] = text;
        }

        return new LocalizedText(copy);
    }

    public bool TryGet(Locale locale, [MaybeNullWhen(false)] out string text) =>
        _values.TryGetValue(locale, out text);
}
