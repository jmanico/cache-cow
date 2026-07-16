using CacheCow.SharedKernel;

namespace CacheCow.Modules.ContentLocalization.Email;

/// <summary>
/// Base of every fully composed email handed to <see cref="IEmailDispatch"/>.
/// Header-safe by construction (SECURITY.md, Input validation rule 10; Email
/// and messaging security rule 1): the ONLY header a composed email ever
/// carries is Content-Language, whose value is the canonical tag of a typed
/// <see cref="Locale"/> — no constructor path lets callers (or user input)
/// add or influence any other header — and the subject is rejected outright
/// if it contains any control character, so a CR/LF header-injection payload
/// is unrepresentable at this type. Bodies are plain text; anything
/// user-influenced lives exclusively in <see cref="TextBody"/>.
/// Subclassing is confined to this module (<c>private protected</c>
/// constructor) so the invariant cannot be widened from outside.
/// </summary>
public abstract class ComposedEmail
{
    private protected ComposedEmail(Locale localeUsed, string subject, string textBody)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(textBody);

        if (subject.Any(char.IsControl))
        {
            throw new ArgumentException(
                "A composed subject must be a single line without control characters (SECURITY.md, Input validation rule 10).",
                nameof(subject));
        }

        LocaleUsed = localeUsed;
        Subject = subject;
        TextBody = textBody;
        Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Language"] = localeUsed.Tag,
        };
    }

    /// <summary>The locale the message actually rendered in.</summary>
    public Locale LocaleUsed { get; }

    public string Subject { get; }

    /// <summary>Plain-text body; the only place user-influenced content may appear.</summary>
    public string TextBody { get; }

    /// <summary>Message headers. Contains Content-Language only — never tokens, links, PII, or user input.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; }
}
