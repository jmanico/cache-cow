using System.Collections.Concurrent;

namespace CacheCow.Modules.MarketGating.Resolution;

/// <summary>
/// In-memory stand-in for <see cref="IMarketPreferenceStore"/> — suitable for
/// tests and local composition only. It is NOT the durable cross-session store
/// CC-MKT-002 ultimately requires: that implementation is blocked on the open
/// residency/write-region decision (ARCHITECTURE.md, "Known unknowns") and
/// must not be improvised here.
/// </summary>
public sealed class InMemoryMarketPreferenceStore : IMarketPreferenceStore
{
    private readonly ConcurrentDictionary<PreferenceSubject, MarketLocalePreference> _preferences = new();

    public MarketLocalePreference? Find(PreferenceSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        return _preferences.TryGetValue(subject, out var preference) ? preference : null;
    }

    public void Save(PreferenceSubject subject, MarketLocalePreference preference)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(preference);
        _preferences[subject] = preference;
    }
}
