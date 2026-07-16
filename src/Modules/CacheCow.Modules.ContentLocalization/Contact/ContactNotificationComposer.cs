using System.Text;
using CacheCow.Modules.ContentLocalization.Email;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.ContentLocalization.Contact;

/// <summary>The forwarded contact notification, ready for the dispatch port. Header safety is inherited from <see cref="ComposedEmail"/>.</summary>
public sealed class ComposedContactNotification : ComposedEmail
{
    internal ComposedContactNotification(Locale localeUsed, string subject, string textBody)
        : base(localeUsed, subject, textBody)
    {
    }
}

/// <summary>
/// Composes the internal notification for an accepted contact submission
/// (CC-CNT-004). Email-header-injection immunity (SECURITY.md, Input
/// validation rule 10) is structural, not filtered:
/// <list type="bullet">
/// <item>The subject is built from a server constant plus the topic, which is
/// a member of the closed <see cref="ContactTopics"/> set — server
/// vocabulary, not user bytes.</item>
/// <item>The submitter's name, reply-to address, and message appear SOLELY as
/// labeled fields in the plain-text BODY. The <see cref="IEmailDispatch"/>
/// message type is header-safe by construction — its only header is
/// Content-Language, minted from a typed <see cref="Locale"/> — and this
/// module deliberately does NOT add a structured reply-to to that contract,
/// because any such field would be materialized as a user-influenced
/// Reply-To SMTP header by the delivery adapter; a labeled body line gives
/// operations staff the same information with zero header surface.</item>
/// <item><see cref="ContactSubmission"/> already rejected every control
/// character, so no field can even attempt a CR/LF fold; the base type's
/// subject guard is a second, independent layer.</item>
/// </list>
/// </summary>
public static class ContactNotificationComposer
{
    /// <summary>
    /// Internal operations mail renders in en-US. [PROVISIONAL] The staff
    /// locale for back-office notifications is not specified anywhere;
    /// localizing an internal tool string set is a content task, not a
    /// compliance one (CC-I18N-001 governs user-facing surfaces).
    /// </summary>
    internal static readonly Locale NotificationLocale = Locale.Parse("en-US");

    internal const string SubjectPrefix = "Contact form submission: ";

    public static ComposedContactNotification Compose(ContactSubmission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);

        // Server constant + closed-set topic only. Never Name/Email/Message.
        var subject = SubjectPrefix + submission.Topic;

        var body = new StringBuilder()
            .Append("A contact form submission was received.\n\n")
            .Append("Name: ").Append(submission.Name).Append('\n')
            .Append("Reply-to: ").Append(submission.ReplyToEmail).Append('\n')
            .Append("Topic: ").Append(submission.Topic).Append('\n')
            .Append('\n')
            .Append("Message:\n")
            .Append(submission.Message)
            .Append('\n');

        return new ComposedContactNotification(NotificationLocale, subject, body.ToString());
    }
}
