using System.Reflection;
using CacheCow.Modules.Invoicing.Delivery;
using CacheCow.Modules.Invoicing.Invoices;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.Invoicing.Tests;

/// <summary>
/// Issue 048: consumer invoice email is link-only (CC-INV-002, ratified
/// 2026-07-15) — no attachment representation exists in the model at all; the
/// link is opaque by construction and never a guessable identifier
/// (CC-ORD-010).
/// </summary>
public sealed class LinkDeliveryTests
{
    // A structurally valid CC-ORD-010-shaped token for tests: ≥ 128 bits
    // worth of non-numeric, non-sequential material. Minting real tokens is
    // Ordering & Payments (issue 042).
    private const string TestCapabilityToken = "tok_4fz8Qk1mN7xW2cVb9sLpD3aH";

    [Fact]
    [Requirement("CC-INV-002")]
    public void Email_model_has_no_attachment_representation()
    {
        // Not "the attachment is empty" — the concept is unrepresentable:
        // no member of any binary/document type, no attachment-named member.
        var members = typeof(InvoiceEmailMessage).GetMembers(BindingFlags.Public | BindingFlags.Instance);

        Assert.DoesNotContain(members, member =>
            member.Name.Contains("Attachment", StringComparison.OrdinalIgnoreCase)
            || member.Name.Contains("Pdf", StringComparison.OrdinalIgnoreCase));

        var propertyTypes = typeof(InvoiceEmailMessage)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.PropertyType);

        Assert.All(propertyTypes, type => Assert.True(
            type != typeof(byte[]) && type != typeof(ReadOnlyMemory<byte>) && type != typeof(Stream),
            "The invoice email model must not be able to carry a document (CC-INV-002)."));
    }

    [Fact]
    [Requirement("CC-INV-002")]
    [Requirement("CC-ORD-007")]
    public void Email_model_carries_no_postal_address_or_order_pii()
    {
        // CC-ORD-007: no full address in email; the email carries recipient,
        // template locale, and the download link — nothing else.
        var propertyNames = typeof(InvoiceEmailMessage)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["DownloadLink", "Locale", "RecipientEmailAddress"], propertyNames);
    }

    [Fact]
    [Requirement("CC-ORD-010")]
    [Requirement("CC-SEC-017")]
    public void Guest_link_derives_only_from_the_capability_token()
    {
        var link = InvoiceDownloadLink.FromCapabilityToken(TestCapabilityToken);

        Assert.Equal(TestCapabilityToken, link.OpaqueAccessKey);
    }

    [Theory]
    [Requirement("CC-ORD-010")]
    [Requirement("CC-INV-002")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1000234")] // order-number shaped: guessable/enumerable
    [InlineData("20260715001")] // sequential invoice-number shaped
    [InlineData("short")] // below the 128-bit entropy floor
    public void Guessable_or_low_entropy_link_material_is_rejected(string value)
    {
        Assert.ThrowsAny<InvalidOperationException>(() => InvoiceDownloadLink.FromCapabilityToken(value));
    }

    [Fact]
    [Requirement("CC-ORD-010")]
    public void No_link_factory_accepts_an_invoice_or_order_number()
    {
        // Opaque by construction: the only inputs any factory accepts are the
        // capability token and the random 128-bit InvoiceId — never the
        // sequential InvoiceNumber or an OrderReference (issue 048, AC-06).
        var factoryParameterTypes = typeof(InvoiceDownloadLink)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .Distinct()
            .ToArray();

        Assert.DoesNotContain(typeof(InvoiceNumber), factoryParameterTypes);
        Assert.DoesNotContain(typeof(OrderReference), factoryParameterTypes);
        Assert.DoesNotContain(typeof(long), factoryParameterTypes);
        Assert.DoesNotContain(typeof(int), factoryParameterTypes);
    }

    [Fact]
    [Requirement("CC-ORD-010")]
    public void Account_link_uses_the_random_invoice_id_not_the_sequential_number()
    {
        var issuer = InvoiceFixtures.NewIssuer();
        var invoice = issuer.Issue(InvoiceFixtures.Draft(
            Market.US, customerAccount: AccountReference.Parse("acct-1")));

        var link = InvoiceDownloadLink.ForAccountHolder(invoice.Id);

        Assert.Equal(invoice.Id.Value, link.OpaqueAccessKey);

        // The access key is the 128-bit random identity (32 hex chars), not
        // the sequential legal number.
        Assert.Equal(32, link.OpaqueAccessKey.Length);
        Assert.All(link.OpaqueAccessKey, c => Assert.True(char.IsAsciiHexDigitLower(c)));
        Assert.NotEqual(
            invoice.Number.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            link.OpaqueAccessKey);
        Assert.NotEqual(invoice.Id, issuer.Issue(InvoiceFixtures.Draft(Market.US)).Id);
    }

    [Fact]
    [Requirement("CC-ORD-010")]
    [Requirement("CC-SEC-017")]
    public void Link_and_guest_request_never_leak_the_token_through_ToString()
    {
        // SECURITY.md, Logging rule 4 / issue 048 AC-07: tokens never reach
        // logs — the diagnostic string form is redacted.
        var link = InvoiceDownloadLink.FromCapabilityToken(TestCapabilityToken);
        var request = new CacheCow.Modules.Invoicing.Access.GuestTokenAccessRequest(TestCapabilityToken);

        Assert.DoesNotContain(TestCapabilityToken, link.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(TestCapabilityToken, request.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-INV-002")]
    public void Email_message_composes_link_only_delivery()
    {
        var email = new InvoiceEmailMessage(
            "customer@example.test",
            Locale.Parse("de-DE"),
            InvoiceDownloadLink.FromCapabilityToken(TestCapabilityToken));

        Assert.Equal("customer@example.test", email.RecipientEmailAddress);
        Assert.Equal(Locale.Parse("de-DE"), email.Locale);
        Assert.Equal(TestCapabilityToken, email.DownloadLink.OpaqueAccessKey);
    }
}
