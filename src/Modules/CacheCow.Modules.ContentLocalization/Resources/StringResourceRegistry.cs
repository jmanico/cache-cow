using CacheCow.Modules.ContentLocalization.Resources.MessageFormat;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.ContentLocalization.Resources;

/// <summary>
/// Validated string resources keyed (resource key, <see cref="Locale"/>) for
/// the seven launch locales (CC-I18N-001/002). A registry can only be built
/// from a resource set that passed every validation rule — key parity,
/// no-HTML, ICU well-formedness, placeholder parity — so everything it hands
/// out is a parsed, validated message (SECURITY.md, Input validation rule 1).
/// </summary>
public sealed class StringResourceRegistry
{
    private readonly IReadOnlyDictionary<(string Key, Locale Locale), IcuMessage> _messages;
    private readonly IReadOnlySet<string> _keys;

    private StringResourceRegistry(
        IReadOnlyDictionary<(string Key, Locale Locale), IcuMessage> messages,
        IReadOnlySet<string> keys)
    {
        _messages = messages;
        _keys = keys;
    }

    /// <summary>All resource keys (identical across locales by key parity).</summary>
    public IReadOnlyCollection<string> Keys => _keys;

    /// <summary>
    /// Validates the set and builds the registry, throwing
    /// <see cref="TranslationValidationException"/> on any violation — the
    /// set is rejected as a whole, never partially accepted (CC-QA-006).
    /// </summary>
    public static StringResourceRegistry Create(TranslationResourceSet set)
    {
        ArgumentNullException.ThrowIfNull(set);

        var result = TranslationResourceValidator.Validate(set);
        if (!result.IsValid)
        {
            throw new TranslationValidationException(result.Violations);
        }

        var messages = new Dictionary<(string, Locale), IcuMessage>();
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (locale, resources) in set.Resources)
        {
            foreach (var (key, value) in resources)
            {
                messages[(key, locale)] = IcuMessageParser.Parse(value);
                keys.Add(key);
            }
        }

        return new StringResourceRegistry(messages, keys);
    }

    public bool Contains(string key, Locale locale) =>
        _messages.ContainsKey((key, locale));

    public bool TryGetMessage(string key, Locale locale, out IcuMessage? message)
    {
        if (_messages.TryGetValue((key, locale), out var found))
        {
            message = found;
            return true;
        }

        message = null;
        return false;
    }
}
