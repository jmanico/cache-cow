using CacheCow.SharedKernel;

namespace CacheCow.Modules.ContentLocalization.Resources;

/// <summary>
/// Format-time entry point over the validated registry. Fallback semantics
/// per CC-I18N-006: when the requested locale has no resource for a key, the
/// message falls back to the market's primary language — a broken template is
/// never rendered. If neither locale resolves, formatting fails closed with
/// <see cref="MessageResourceMissingException"/> rather than emitting a key
/// name or partial output. (Runtime fallback for UI strings beyond email
/// templates is an issue 064 open question; these are the CC-I18N-006
/// semantics applied at format time.)
/// </summary>
public sealed class LocalizedMessageFormatter
{
    private readonly StringResourceRegistry _registry;
    private readonly MarketPrimaryLocales _primaryLocales;

    public LocalizedMessageFormatter(StringResourceRegistry registry, MarketPrimaryLocales primaryLocales)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(primaryLocales);
        _registry = registry;
        _primaryLocales = primaryLocales;
    }

    /// <summary>
    /// Formats (key, requestedLocale), falling back to the market's primary
    /// language when the requested locale has no resource for the key.
    /// Returns the formatted text and the locale actually used.
    /// </summary>
    public (string Text, Locale LocaleUsed) Format(
        string key,
        Locale requestedLocale,
        Market market,
        IReadOnlyDictionary<string, object?> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(arguments);

        var locale = ResolveLocale(key, requestedLocale, market);
        if (!_registry.TryGetMessage(key, locale, out var message))
        {
            throw new MessageResourceMissingException(key, locale);
        }

        return (message!.Format(locale, arguments), locale);
    }

    /// <summary>
    /// Resolves which locale a key would render in for the requested
    /// locale/market pair, applying CC-I18N-006 fallback without formatting.
    /// </summary>
    public Locale ResolveLocale(string key, Locale requestedLocale, Market market)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_registry.Contains(key, requestedLocale))
        {
            return requestedLocale;
        }

        // Fallback is the market's primary language (CC-I18N-006). For a
        // market whose primary is an open decision (IN, unconfigured) this
        // fails closed rather than guessing.
        var primary = _primaryLocales.GetPrimaryLocale(market);
        if (_registry.Contains(key, primary))
        {
            return primary;
        }

        throw new MessageResourceMissingException(key, primary);
    }
}

/// <summary>
/// No template exists for the key in either the requested locale or the
/// market's primary-language fallback: fail closed — never render a broken
/// template or leak the raw key to a user surface (CC-I18N-006).
/// </summary>
public sealed class MessageResourceMissingException : InvalidOperationException
{
    public MessageResourceMissingException(string key, Locale locale)
        : base($"No string resource for key '{key}' in locale '{locale.Tag}' or its fallback; refusing to render a broken template (CC-I18N-006).")
    {
    }
}
