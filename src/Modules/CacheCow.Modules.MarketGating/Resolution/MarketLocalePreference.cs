using CacheCow.SharedKernel;

namespace CacheCow.Modules.MarketGating.Resolution;

/// <summary>
/// A user's persisted explicit market/locale choice (CC-MKT-002). Either half
/// may be unset — market and locale are independent selections (CC-I18N-001)
/// and are never inferred from one another (DESIGN.md §7).
/// </summary>
public sealed record MarketLocalePreference(Market? Market, Locale? Locale);
