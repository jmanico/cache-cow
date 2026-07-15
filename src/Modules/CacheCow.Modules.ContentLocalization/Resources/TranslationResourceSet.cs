using CacheCow.SharedKernel;

namespace CacheCow.Modules.ContentLocalization.Resources;

/// <summary>
/// A raw, not-yet-trusted set of string resources: per locale, a map of
/// resource key to ICU MessageFormat source string. This is the shape in
/// which translation files cross the trust boundary — attacker-controlled
/// until validated (SECURITY.md, Input validation rules 1 and 7); only
/// <see cref="StringResourceRegistry.Create"/> turns it into something the
/// platform will format from.
/// </summary>
public sealed class TranslationResourceSet
{
    public TranslationResourceSet(IReadOnlyDictionary<Locale, IReadOnlyDictionary<string, string>> resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        Resources = resources;
    }

    /// <summary>Per-locale key → raw ICU message source.</summary>
    public IReadOnlyDictionary<Locale, IReadOnlyDictionary<string, string>> Resources { get; }

    /// <summary>Convenience factory from string locale tags (e.g. deserialized resource files).</summary>
    public static TranslationResourceSet FromTags(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> resourcesByTag)
    {
        ArgumentNullException.ThrowIfNull(resourcesByTag);

        var typed = new Dictionary<Locale, IReadOnlyDictionary<string, string>>();
        foreach (var (tag, keys) in resourcesByTag)
        {
            typed[Locale.Parse(tag)] = keys;
        }

        return new TranslationResourceSet(typed);
    }
}
