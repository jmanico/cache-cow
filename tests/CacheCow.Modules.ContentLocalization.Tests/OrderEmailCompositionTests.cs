using System.Reflection;
using CacheCow.Modules.ContentLocalization.Email;
using CacheCow.Modules.ContentLocalization.Resources;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.ContentLocalization.Tests;

/// <summary>
/// Issue 043: order confirmation and shipment notification compose from
/// validated resources in all seven launch locales, fall back to the market's
/// primary language (never a broken or mixed template, CC-I18N-006), carry no
/// full delivery address (unrepresentable by type shape, CC-ORD-007), and put
/// no capability token or link into headers/metadata (SECURITY.md, Email and
/// messaging security rule 1).
/// </summary>
public sealed class OrderEmailCompositionTests
{
    private const string TrackingLink = "https://track.cachecow.example/t/opaque-capability-token-value";

    private static readonly StringResourceRegistry Registry =
        StringResourceRegistry.Create(PlaceholderOrderEmailResources.Set);

    private static OrderEmailComposer Composer(MarketPrimaryLocales? primaries = null) =>
        new(Registry, primaries ?? MarketPrimaryLocales.Default);

    private static OrderEmailSummary Summary(string? trackingLink = null) =>
        new(
            "CC-2026-000123",
            [new OrderEmailLine("Smoked Brisket, 1 kg", 2), new OrderEmailLine("Paneer Burnt Ends", 1)],
            "$149.00",
            trackingLink);

    [Theory]
    [Requirement("CC-ORD-007")]
    [Requirement("CC-I18N-006")]
    [InlineData("en-US", "US")]
    [InlineData("es-ES", "ES")]
    [InlineData("es-MX", "MX")]
    [InlineData("de-DE", "DE")]
    [InlineData("ja-JP", "JP")]
    [InlineData("en-IN", "IN")]
    [InlineData("hi-IN", "IN")]
    public void Both_email_kinds_compose_in_every_launch_locale(string localeTag, string marketCode)
    {
        var locale = Locale.Parse(localeTag);
        var market = Market.Parse(marketCode);
        var composer = Composer();

        var confirmation = composer.Compose(OrderEmailKind.OrderConfirmation, Summary(), locale, market);
        var shipment = composer.Compose(OrderEmailKind.ShipmentNotification, Summary(TrackingLink), locale, market);

        // The requested locale is a launch locale with full templates, so no
        // fallback occurs — even for IN, whose primary language is undecided.
        Assert.Equal(locale, confirmation.LocaleUsed);
        Assert.Equal(locale, shipment.LocaleUsed);
        Assert.Contains("CC-2026-000123", confirmation.Subject, StringComparison.Ordinal);
        Assert.Contains("Smoked Brisket, 1 kg", confirmation.TextBody, StringComparison.Ordinal);
        Assert.Contains("$149.00", confirmation.TextBody, StringComparison.Ordinal);
        Assert.Contains(TrackingLink, shipment.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-I18N-006")]
    public void An_unsupported_locale_falls_back_to_the_markets_primary_language_as_a_whole_template()
    {
        var email = Composer().Compose(
            OrderEmailKind.OrderConfirmation, Summary(), Locale.Parse("fr-FR"), Market.DE);

        Assert.Equal(Locale.Parse("de-DE"), email.LocaleUsed);
        Assert.Contains("Bestellung CC-2026-000123 bestätigt", email.Subject, StringComparison.Ordinal);
        // Whole-template fallback: nothing rendered from any other locale.
        Assert.Contains("Gesamtbetrag:", email.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Order total:", email.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-I18N-006")]
    public void Fallback_for_the_IN_market_fails_closed_until_its_primary_language_is_decided()
    {
        Assert.Throws<MarketPrimaryLocaleUndecidedException>(() => Composer().Compose(
            OrderEmailKind.OrderConfirmation, Summary(), Locale.Parse("fr-FR"), Market.IN));

        var configured = Composer(MarketPrimaryLocales.WithIndiaPrimary(Locale.Parse("hi-IN")));
        var email = configured.Compose(
            OrderEmailKind.OrderConfirmation, Summary(), Locale.Parse("fr-FR"), Market.IN);
        Assert.Equal(Locale.Parse("hi-IN"), email.LocaleUsed);
    }

    [Fact]
    [Requirement("CC-ORD-007")]
    public void The_email_input_type_cannot_represent_a_delivery_address()
    {
        // Data minimization is enforced by shape: no property on the summary,
        // its lines, or the composed email can carry address data.
        string[] forbiddenFragments = ["address", "street", "city", "postal", "zip", "pin", "prefecture", "state", "country"];

        var surface = new[] { typeof(OrderEmailSummary), typeof(OrderEmailLine), typeof(ComposedOrderEmail) }
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Select(p => p.Name)
            .ToArray();

        foreach (var property in surface)
        {
            foreach (var fragment in forbiddenFragments)
            {
                Assert.False(
                    property.Contains(fragment, StringComparison.OrdinalIgnoreCase),
                    $"Property '{property}' looks like address data; CC-ORD-007 forbids a full delivery address in email.");
            }
        }
    }

    [Fact]
    [Requirement("CC-ORD-007")]
    public void Headers_carry_only_content_language_never_the_tracking_link_or_tokens()
    {
        var email = Composer().Compose(
            OrderEmailKind.ShipmentNotification, Summary(TrackingLink), Locale.Parse("en-US"), Market.US);

        Assert.Equal(["Content-Language"], email.Headers.Keys.ToArray());
        Assert.Equal("en-US", email.Headers["Content-Language"]);
        foreach (var value in email.Headers.Values)
        {
            Assert.DoesNotContain("opaque-capability-token-value", value, StringComparison.OrdinalIgnoreCase);
        }

        // The capability-bearing link exists exactly once: in the body.
        Assert.DoesNotContain(TrackingLink, email.Subject, StringComparison.Ordinal);
        Assert.Contains(TrackingLink, email.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-ORD-007")]
    public void A_confirmation_must_not_carry_a_tracking_link_and_a_shipment_requires_one()
    {
        var composer = Composer();

        Assert.Throws<ArgumentException>(() => composer.Compose(
            OrderEmailKind.OrderConfirmation, Summary(TrackingLink), Locale.Parse("en-US"), Market.US));
        Assert.Throws<ArgumentException>(() => composer.Compose(
            OrderEmailKind.ShipmentNotification, Summary(), Locale.Parse("en-US"), Market.US));
    }

    [Theory]
    [Requirement("CC-ORD-007")]
    [InlineData("http://insecure.example/track")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not a url")]
    public void The_tracking_link_must_be_an_absolute_https_url(string link)
    {
        Assert.Throws<ArgumentException>(() => Summary(link));
    }

    [Fact]
    [Requirement("CC-ORD-007")]
    public void Inputs_with_control_characters_are_rejected_before_composition()
    {
        // Header-injection hardening: no input can smuggle CR/LF toward SMTP
        // headers (SECURITY.md, Input validation rule 10).
        Assert.Throws<ArgumentException>(() => new OrderEmailSummary(
            "CC-1\r\nBcc: attacker@evil.example", [new OrderEmailLine("Brisket", 1)], "$1.00"));
        Assert.Throws<ArgumentException>(() => new OrderEmailLine("Brisket\r\nX-Evil: 1", 1));
        Assert.Throws<ArgumentException>(() => new OrderEmailSummary(
            "CC-1", [new OrderEmailLine("Brisket", 1)], "$1.00\n"));
    }

    [Fact]
    [Requirement("CC-ORD-007")]
    public void A_hostile_item_name_renders_inert_in_the_body()
    {
        var summary = new OrderEmailSummary(
            "CC-1", [new OrderEmailLine("<script>alert(1)</script> Brisket {orderReference}", 1)], "$1.00");

        var email = Composer().Compose(OrderEmailKind.OrderConfirmation, summary, Locale.Parse("en-US"), Market.US);

        // Inserted as plain text by the escape-by-default pipeline: present
        // verbatim, never interpreted as markup or ICU syntax.
        Assert.Contains("<script>alert(1)</script> Brisket {orderReference}", email.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("CC-1 Brisket", email.TextBody, StringComparison.Ordinal);
    }

    [Theory]
    [Requirement("CC-I18N-006")]
    [InlineData("en-US", 1, "1 pack")]
    [InlineData("en-US", 3, "3 packs")]
    [InlineData("de-DE", 3, "3 Pakete")]
    [InlineData("ja-JP", 3, "3点")]
    public void Line_quantities_pluralize_per_locale(string localeTag, int quantity, string expected)
    {
        var market = localeTag == "de-DE" ? Market.DE : localeTag == "ja-JP" ? Market.JP : Market.US;
        var summary = new OrderEmailSummary("CC-1", [new OrderEmailLine("Brisket", quantity)], "$1.00");

        var email = Composer().Compose(OrderEmailKind.OrderConfirmation, summary, Locale.Parse(localeTag), market);

        Assert.Contains(expected, email.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-ORD-007")]
    public async Task The_service_composes_and_hands_off_to_the_dispatch_port()
    {
        var dispatch = new InMemoryEmailDispatch();
        var service = new OrderEmailService(Composer(), dispatch);

        var composed = await service.SendAsync(
            OrderEmailKind.OrderConfirmation,
            Summary(),
            "customer@example.com",
            Locale.Parse("es-MX"),
            Market.MX,
            TestContext.Current.CancellationToken);

        var dispatched = Assert.Single(dispatch.Dispatched);
        Assert.Same(composed, dispatched.Email);
        Assert.Equal("customer@example.com", dispatched.RecipientEmailAddress);
        Assert.Equal(Locale.Parse("es-MX"), composed.LocaleUsed);
    }

    [Fact]
    [Requirement("CC-ORD-007")]
    public async Task A_recipient_address_with_control_characters_is_rejected()
    {
        var service = new OrderEmailService(Composer(), new InMemoryEmailDispatch());

        await Assert.ThrowsAsync<ArgumentException>(() => service.SendAsync(
            OrderEmailKind.OrderConfirmation,
            Summary(),
            "victim@example.com\r\nBcc: everyone@example.com",
            Locale.Parse("en-US"),
            Market.US,
            TestContext.Current.CancellationToken));
    }
}
