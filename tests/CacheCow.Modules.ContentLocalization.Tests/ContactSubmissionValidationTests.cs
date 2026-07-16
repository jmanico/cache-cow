using CacheCow.Modules.ContentLocalization.Contact;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.ContentLocalization.Tests;

/// <summary>
/// Issue 076 (CC-CNT-004; SECURITY.md, Input validation rules 1 and 10):
/// the contact-submission schema is enforced server-side by construction —
/// bounded lengths, no control characters in any field (CR/LF header-injection
/// bytes rejected outright, never stripped), plain text only, strict reply-to
/// syntax, closed topic set. Invalid input is rejected, never sanitized into
/// acceptance.
/// </summary>
public sealed class ContactSubmissionValidationTests
{
    private const string ValidName = "Ada Lovelace";
    private const string ValidEmail = "ada@example.com";
    private const string ValidTopic = "order";
    private const string ValidMessage = "Where is my brisket order? It was due yesterday.";

    private static ContactSubmissionRejection Reject(
        string? name = ValidName,
        string? email = ValidEmail,
        string? topic = ValidTopic,
        string? message = ValidMessage)
    {
        var accepted = ContactSubmission.TryCreate(name, email, topic, message, out var submission, out var rejection);
        Assert.Equal(accepted, rejection == ContactSubmissionRejection.None);
        Assert.Equal(accepted, submission is not null);
        return rejection;
    }

    [Fact]
    [Requirement("CC-CNT-004")]
    public void A_well_formed_submission_is_accepted_with_every_field_preserved_verbatim()
    {
        var accepted = ContactSubmission.TryCreate(
            ValidName, ValidEmail, ValidTopic, ValidMessage, out var submission, out var rejection);

        Assert.True(accepted);
        Assert.Equal(ContactSubmissionRejection.None, rejection);
        Assert.Equal(ValidName, submission!.Name);
        Assert.Equal(ValidEmail, submission.ReplyToEmail);
        Assert.Equal(ValidTopic, submission.Topic);
        Assert.Equal(ValidMessage, submission.Message);
    }

    [Fact]
    [Requirement("CC-CNT-004")]
    public void Every_topic_in_the_closed_set_is_accepted()
    {
        Assert.All(ContactTopics.All, topic =>
            Assert.Equal(ContactSubmissionRejection.None, Reject(topic: topic)));
    }

    // ---- missing fields -----------------------------------------------------

    [Theory]
    [Requirement("CC-CNT-004")]
    [Requirement("CC-SEC-001")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_name_email_topic_or_message_is_rejected(string? absent)
    {
        Assert.Equal(ContactSubmissionRejection.MissingField, Reject(name: absent));
        Assert.Equal(ContactSubmissionRejection.MissingField, Reject(email: absent));
        Assert.Equal(ContactSubmissionRejection.MissingField, Reject(topic: absent));
        Assert.Equal(ContactSubmissionRejection.MissingField, Reject(message: absent));
    }

    // ---- bounded lengths ----------------------------------------------------

    [Fact]
    [Requirement("CC-CNT-004")]
    [Requirement("CC-SEC-001")]
    public void Fields_exceeding_their_bounds_are_rejected_and_boundary_lengths_are_accepted()
    {
        Assert.Equal(ContactSubmissionRejection.FieldTooLong, Reject(name: new string('a', ContactSubmission.MaxNameLength + 1)));
        Assert.Equal(ContactSubmissionRejection.None, Reject(name: new string('a', ContactSubmission.MaxNameLength)));

        var longEmail = new string('a', ContactSubmission.MaxEmailLength - "@example.com".Length + 1) + "@example.com";
        Assert.Equal(ContactSubmissionRejection.FieldTooLong, Reject(email: longEmail));

        Assert.Equal(ContactSubmissionRejection.FieldTooLong, Reject(message: new string('m', ContactSubmission.MaxMessageLength + 1)));
        Assert.Equal(ContactSubmissionRejection.None, Reject(message: new string('m', ContactSubmission.MaxMessageLength)));
    }

    // ---- control characters and CRLF (header-injection byte class) ----------

    [Theory]
    [Requirement("CC-CNT-004")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    [InlineData("\n")]
    [InlineData("\t")]
    [InlineData("\0")]
    [InlineData("")]
    public void Any_control_character_in_any_field_is_rejected_never_stripped(string controlSequence)
    {
        // The classic header-injection payload shape in every field
        // (issue 076 AC-02: CRLF corpus across the whole DTO).
        var payload = "x" + controlSequence + "Bcc: everyone@example.com";

        Assert.Equal(ContactSubmissionRejection.ControlCharacters, Reject(name: payload));
        Assert.Equal(ContactSubmissionRejection.ControlCharacters, Reject(email: payload));
        Assert.Equal(ContactSubmissionRejection.ControlCharacters, Reject(topic: payload));
        Assert.Equal(ContactSubmissionRejection.ControlCharacters, Reject(message: payload));
    }

    [Fact]
    [Requirement("CC-CNT-004")]
    public void A_crlf_payload_prefixed_to_a_valid_topic_is_rejected_not_normalized()
    {
        Assert.Equal(ContactSubmissionRejection.ControlCharacters, Reject(topic: "order\r\nX-Spam: yes"));
    }

    // ---- plain text only ----------------------------------------------------

    [Theory]
    [Requirement("CC-CNT-004")]
    [Requirement("CC-SEC-001")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("hello <b>world</b>")]
    [InlineData("<")]
    public void Markup_in_the_name_or_message_is_rejected_like_the_translation_rule(string markup)
    {
        Assert.Equal(ContactSubmissionRejection.MarkupNotPermitted, Reject(name: markup));
        Assert.Equal(ContactSubmissionRejection.MarkupNotPermitted, Reject(message: markup));
    }

    // ---- reply-to syntax ----------------------------------------------------

    [Theory]
    [Requirement("CC-CNT-004")]
    [Requirement("CC-SEC-001")]
    [InlineData("plainaddress")]
    [InlineData("@example.com")]
    [InlineData("ada@")]
    [InlineData("ada@@example.com")]
    [InlineData("ada@example")] // no dotted domain
    [InlineData("ada@example..com")]
    [InlineData("ada@-example.com")]
    [InlineData("ada@example-.com")]
    [InlineData(".ada@example.com")]
    [InlineData("ada.@example.com")]
    [InlineData("a..da@example.com")]
    [InlineData("ada lovelace@example.com")]
    [InlineData("\"Ada\" <ada@example.com>")] // display-name form: rejected, not parsed
    [InlineData("ada@example.com>bcc")]
    [InlineData("ádá@example.com")] // non-ASCII local part: allowlist posture
    public void A_malformed_reply_to_address_is_rejected(string email)
    {
        Assert.Equal(ContactSubmissionRejection.MalformedEmailAddress, Reject(email: email));
    }

    [Theory]
    [Requirement("CC-CNT-004")]
    [InlineData("ada@example.com")]
    [InlineData("ada.lovelace+orders@mail.example.co")]
    [InlineData("a_d-a%1@ex-ample.example.com")]
    public void A_well_formed_reply_to_address_is_accepted(string email)
    {
        Assert.Equal(ContactSubmissionRejection.None, Reject(email: email));
    }

    // ---- closed topic set ---------------------------------------------------

    [Theory]
    [Requirement("CC-CNT-004")]
    [Requirement("CC-SEC-001")]
    [InlineData("beef")]
    [InlineData("Order")] // exact ordinal match: no case folding into acceptance
    [InlineData("order ")]
    [InlineData("orders")]
    public void A_topic_outside_the_closed_set_is_rejected(string topic)
    {
        Assert.Equal(ContactSubmissionRejection.UnknownTopic, Reject(topic: topic));
    }
}
