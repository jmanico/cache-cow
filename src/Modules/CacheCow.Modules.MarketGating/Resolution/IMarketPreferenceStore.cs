namespace CacheCow.Modules.MarketGating.Resolution;

/// <summary>
/// Port for persisting a user's explicit market/locale choice across sessions
/// (CC-MKT-002). The durable implementation is deliberately absent: persisting
/// the choice against an EU or India user account is a write of personal data,
/// entangled with the unresolved data-residency vs. single-primary-write-region
/// decision (ARCHITECTURE.md, "Known unknowns"; issue 024 AT RISK). Only the
/// in-memory stand-in (<see cref="InMemoryMarketPreferenceStore"/>) exists
/// until a human resolves that decision. Stored values are typed launch-set
/// values by construction; anything client-supplied is validated by
/// <see cref="TransactingContextResolver"/> before it gets here (issue 024 AC-07).
/// </summary>
public interface IMarketPreferenceStore
{
    /// <summary>The persisted preference for the subject, or null when none exists.</summary>
    MarketLocalePreference? Find(PreferenceSubject subject);

    /// <summary>Persists the subject's explicit choice so it survives across sessions (CC-MKT-002).</summary>
    void Save(PreferenceSubject subject, MarketLocalePreference preference);
}
