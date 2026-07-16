namespace CacheCow.Modules.ContentLocalization.Contact;

/// <summary>
/// The closed set of contact-form topics (CC-CNT-004). A submission naming
/// any other topic is rejected, never accepted provisionally (SECURITY.md,
/// Input validation rule 1). Values are stable machine identifiers — the
/// localized display labels live in the string-resource pipeline
/// (CC-I18N-002), never here. Because the set is closed, an accepted topic is
/// server-controlled vocabulary, not free user text, and is therefore safe in
/// the composed notification subject.
/// [PROVISIONAL] The form's field set is a product decision the specs do not
/// enumerate (issue 076, Open Questions); this set is a structurally complete
/// placeholder and may be re-ratified without touching validation logic.
/// </summary>
public static class ContactTopics
{
    public static IReadOnlyList<string> All { get; } =
    [
        "order",
        "shipping",
        "wholesale",
        "press",
        "privacy",
        "other",
    ];

    /// <summary>Exact ordinal membership — no case folding or trimming into acceptance.</summary>
    public static bool Contains(string topic) => All.Contains(topic, StringComparer.Ordinal);
}
