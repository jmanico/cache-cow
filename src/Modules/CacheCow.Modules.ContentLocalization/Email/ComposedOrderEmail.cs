using CacheCow.SharedKernel;

namespace CacheCow.Modules.ContentLocalization.Email;

/// <summary>
/// A fully composed transactional order email, ready for the dispatch port.
/// Header safety (Content-Language only; control-character-free subject) is
/// inherited from <see cref="ComposedEmail"/>; capability tokens and other
/// secrets never enter headers or metadata (SECURITY.md, Email and messaging
/// security rule 1) — the tracking link, which may be capability-bearing,
/// exists solely inside <see cref="ComposedEmail.TextBody"/>. The body is
/// plain text: string resources cannot contain HTML (SECURITY.md, Input
/// validation rule 7), so no HTML body variant exists here.
/// </summary>
public sealed class ComposedOrderEmail : ComposedEmail
{
    internal ComposedOrderEmail(OrderEmailKind kind, Locale localeUsed, string subject, string textBody)
        : base(localeUsed, subject, textBody)
    {
        Kind = kind;
    }

    public OrderEmailKind Kind { get; }
}
