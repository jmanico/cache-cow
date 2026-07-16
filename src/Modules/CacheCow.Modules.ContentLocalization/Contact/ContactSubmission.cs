namespace CacheCow.Modules.ContentLocalization.Contact;

/// <summary>
/// Why a contact submission was rejected. Logged as a server-controlled
/// constant (SECURITY.md, Logging rules 4–5) — never alongside the submitted
/// values — while the client sees one generic RFC 9457 problem regardless of
/// reason, so rejections leak no validation oracle and echo nothing back.
/// </summary>
public enum ContactSubmissionRejection
{
    None = 0,

    /// <summary>A required field is missing, empty, or whitespace-only.</summary>
    MissingField,

    /// <summary>A field exceeds its bounded length.</summary>
    FieldTooLong,

    /// <summary>
    /// A field contains a control character. This includes CR/LF anywhere in
    /// any field — the email-header-injection payload class (CWE-93) — which
    /// is rejected outright, never stripped (SECURITY.md, Input validation
    /// rules 1 and 10).
    /// </summary>
    ControlCharacters,

    /// <summary>A field contains '&lt;': submissions are plain text only, mirroring the no-HTML rule for string resources (SECURITY.md, Input validation rule 7).</summary>
    MarkupNotPermitted,

    /// <summary>The reply-to address fails strict syntax validation.</summary>
    MalformedEmailAddress,

    /// <summary>The topic is not in the closed <see cref="ContactTopics"/> set.</summary>
    UnknownTopic,
}

/// <summary>
/// A validated contact submission (CC-CNT-004): construction is only possible
/// through <see cref="TryCreate"/>, so an instance proves every field passed
/// the server-side schema — bounded lengths, no control characters anywhere
/// (hence no CR/LF header-injection bytes), plain text only, a
/// syntax-validated reply-to address, and a topic from the closed set.
/// Invalid input is rejected, never sanitized into acceptance (SECURITY.md,
/// Input validation rule 1). Field values are Restricted/PII: they may enter
/// the composed notification BODY, never SMTP headers and never log entries.
/// </summary>
public sealed class ContactSubmission
{
    public const int MaxNameLength = 100;

    /// <summary>RFC 5321 path length bound.</summary>
    public const int MaxEmailLength = 254;

    public const int MaxMessageLength = 4000;

    private ContactSubmission(string name, string replyToEmail, string topic, string message)
    {
        Name = name;
        ReplyToEmail = replyToEmail;
        Topic = topic;
        Message = message;
    }

    /// <summary>Submitter display name. Free text (bounded, control-character-free, no markup) — body-only downstream.</summary>
    public string Name { get; }

    /// <summary>Where operations staff reply. Syntax-validated; still body-only downstream — it never becomes a Reply-To header.</summary>
    public string ReplyToEmail { get; }

    /// <summary>A member of <see cref="ContactTopics.All"/> — server vocabulary, not user text.</summary>
    public string Topic { get; }

    /// <summary>Plain-text message body (bounded, control-character-free, no markup).</summary>
    public string Message { get; }

    public static bool TryCreate(
        string? name,
        string? replyToEmail,
        string? topic,
        string? message,
        out ContactSubmission? submission,
        out ContactSubmissionRejection rejection)
    {
        submission = null;

        rejection = CheckCommon(name, MaxNameLength);
        if (rejection == ContactSubmissionRejection.None && name!.Contains('<', StringComparison.Ordinal))
        {
            rejection = ContactSubmissionRejection.MarkupNotPermitted;
        }

        if (rejection != ContactSubmissionRejection.None)
        {
            return false;
        }

        rejection = CheckCommon(replyToEmail, MaxEmailLength);
        if (rejection == ContactSubmissionRejection.None && !IsValidEmailAddressSyntax(replyToEmail!))
        {
            rejection = ContactSubmissionRejection.MalformedEmailAddress;
        }

        if (rejection != ContactSubmissionRejection.None)
        {
            return false;
        }

        rejection = CheckCommon(topic, MaxNameLength);
        if (rejection == ContactSubmissionRejection.None && !ContactTopics.Contains(topic!))
        {
            rejection = ContactSubmissionRejection.UnknownTopic;
        }

        if (rejection != ContactSubmissionRejection.None)
        {
            return false;
        }

        rejection = CheckCommon(message, MaxMessageLength);
        if (rejection == ContactSubmissionRejection.None && message!.Contains('<', StringComparison.Ordinal))
        {
            rejection = ContactSubmissionRejection.MarkupNotPermitted;
        }

        if (rejection != ContactSubmissionRejection.None)
        {
            return false;
        }

        submission = new ContactSubmission(name!, replyToEmail!, topic!, message!);
        return true;
    }

    /// <summary>
    /// Field-agnostic gate: present, bounded, and free of every control
    /// character. CR and LF are control characters, so the header-injection
    /// byte class is rejected in every field — including the message, whose
    /// multiline support is an open UX/product question (issue 076: the field
    /// set itself is undecided); until a human ratifies a multiline message
    /// field, the strict single-line posture stands.
    /// </summary>
    private static ContactSubmissionRejection CheckCommon(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ContactSubmissionRejection.MissingField;
        }

        if (value.Any(char.IsControl))
        {
            return ContactSubmissionRejection.ControlCharacters;
        }

        if (value.Length > maxLength)
        {
            return ContactSubmissionRejection.FieldTooLong;
        }

        return ContactSubmissionRejection.None;
    }

    /// <summary>
    /// Strict first-party address syntax check (allowlist posture, SECURITY.md,
    /// Input validation rule 1): ASCII local part of letters, digits, and
    /// <c>._%+-</c> (max 64, no leading/trailing/double dot), exactly one '@',
    /// and a dotted domain of letter/digit/hyphen labels. Exotic-but-valid RFC
    /// 5321 forms (quoted local parts, address literals, display names like
    /// <c>"a &lt;b@c&gt;"</c>) are deliberately rejected — this is the field a
    /// header-injection attack targets, and rejecting is safer than parsing.
    /// </summary>
    internal static bool IsValidEmailAddressSyntax(string value)
    {
        if (value.Length > MaxEmailLength || value.Any(char.IsControl))
        {
            return false;
        }

        var at = value.IndexOf('@');
        if (at <= 0 || at != value.LastIndexOf('@') || at == value.Length - 1)
        {
            return false;
        }

        var local = value[..at];
        if (local.Length > 64
            || local[0] == '.'
            || local[^1] == '.'
            || local.Contains("..", StringComparison.Ordinal)
            || !local.All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '%' or '+' or '-'))
        {
            return false;
        }

        var labels = value[(at + 1)..].Split('.');
        if (labels.Length < 2)
        {
            return false;
        }

        foreach (var label in labels)
        {
            if (label.Length is 0 or > 63
                || label[0] == '-'
                || label[^1] == '-'
                || !label.All(static c => char.IsAsciiLetterOrDigit(c) || c == '-'))
            {
                return false;
            }
        }

        return true;
    }
}
