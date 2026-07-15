using CacheCow.SharedKernel;

namespace CacheCow.Modules.ContentLocalization.Email;

/// <summary>
/// A fully composed transactional email, ready for the dispatch port. The
/// only header the composition produces is Content-Language; capability
/// tokens and other secrets never enter headers or metadata (SECURITY.md,
/// Email and messaging security rule 1) — the tracking link, which may be
/// capability-bearing, exists solely inside <see cref="TextBody"/>. The body
/// is plain text: string resources cannot contain HTML (SECURITY.md, Input
/// validation rule 7), so no HTML body variant exists here.
/// </summary>
public sealed class ComposedOrderEmail
{
    internal ComposedOrderEmail(OrderEmailKind kind, Locale localeUsed, string subject, string textBody)
    {
        if (subject.Any(char.IsControl))
        {
            throw new ArgumentException(
                "A composed subject must be a single line without control characters (SECURITY.md, Input validation rule 10).",
                nameof(subject));
        }

        Kind = kind;
        LocaleUsed = localeUsed;
        Subject = subject;
        TextBody = textBody;
        Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Language"] = localeUsed.Tag,
        };
    }

    public OrderEmailKind Kind { get; }

    /// <summary>The locale the whole template actually rendered in (requested locale or the market-primary fallback, CC-I18N-006).</summary>
    public Locale LocaleUsed { get; }

    public string Subject { get; }

    public string TextBody { get; }

    /// <summary>Message headers. Contains Content-Language only — never tokens, links, or PII.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; }
}
