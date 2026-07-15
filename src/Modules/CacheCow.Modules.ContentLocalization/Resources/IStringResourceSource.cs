namespace CacheCow.Modules.ContentLocalization.Resources;

/// <summary>
/// Port for loading raw translation resources (CC-I18N-002). Adapters for
/// resource files or CMS-backed sources land with later issues; whatever the
/// source, the loaded set is untrusted until it passes
/// <see cref="TranslationResourceValidator"/> (SECURITY.md, Input validation
/// rules 1 and 7).
/// </summary>
public interface IStringResourceSource
{
    TranslationResourceSet Load();
}

/// <summary>In-memory source for tests and the provisional module default.</summary>
public sealed class InMemoryStringResourceSource : IStringResourceSource
{
    private readonly TranslationResourceSet _set;

    public InMemoryStringResourceSource(TranslationResourceSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        _set = set;
    }

    public TranslationResourceSet Load() => _set;
}
