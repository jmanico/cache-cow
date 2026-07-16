namespace CacheCow.Modules.ContentLocalization.Contact;

/// <summary>
/// The named rate-limiter policy the contact endpoint attaches as metadata
/// (CC-CNT-004; SECURITY.md, HTTP boundary rule 7: public forms are
/// rate-limited, stricter than the baseline). The HOST owns the limiter
/// middleware and its 429 + Retry-After semantics (issue 019) and must
/// register a policy by this exact name, following the host's existing
/// kebab-case policy naming ("authentication", "order-creation"). The numeric
/// budget is NOT ratified anywhere in the specs — it is host configuration
/// awaiting a human decision, not a constant in this module.
/// </summary>
public static class ContactRateLimitPolicies
{
    /// <summary>Contact-form submissions: stricter than the anonymous baseline; budget is unratified host configuration.</summary>
    public const string ContactForm = "contact-form";
}
