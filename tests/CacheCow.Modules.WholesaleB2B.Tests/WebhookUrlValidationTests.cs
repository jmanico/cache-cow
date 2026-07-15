using System.Net;
using CacheCow.Modules.WholesaleB2B.Webhooks;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.WholesaleB2B.Tests;

/// <summary>
/// Issue 057 (CC-API-009; SECURITY.md, Input validation rule 8): the SSRF
/// policy for partner receiver URLs — HTTPS only, no embedded credentials,
/// public addresses only across IPv4, IPv6, mapped, and NAT64 encodings, with
/// resolution-based (not string-matching) validation that fails closed.
/// </summary>
public sealed class WebhookUrlValidationTests
{
    private static readonly FakeWebhookAddressResolver PublicResolver = new();

    [Theory]
    [InlineData("http://partner.example.com/hooks")] // scheme
    [InlineData("ftp://partner.example.com/hooks")] // scheme
    [InlineData("https://user:secret@partner.example.com/hooks")] // credentials
    [InlineData("https://localhost/hooks")] // loopback name resolves via resolver below
    [InlineData("https://127.0.0.1/hooks")] // IPv4 loopback literal
    [InlineData("https://127.8.9.10/hooks")] // loopback range
    [InlineData("https://10.0.0.8/hooks")] // private
    [InlineData("https://172.16.4.4/hooks")] // private
    [InlineData("https://172.31.255.1/hooks")] // private upper bound
    [InlineData("https://192.168.1.1/hooks")] // private
    [InlineData("https://100.64.0.1/hooks")] // CGNAT
    [InlineData("https://169.254.169.254/hooks")] // cloud metadata
    [InlineData("https://169.254.1.1/hooks")] // link-local
    [InlineData("https://0.0.0.0/hooks")] // unspecified
    [InlineData("https://255.255.255.255/hooks")] // broadcast
    [InlineData("https://224.0.0.1/hooks")] // multicast
    [InlineData("https://[::1]/hooks")] // IPv6 loopback
    [InlineData("https://[::]/hooks")] // IPv6 unspecified
    [InlineData("https://[fc00::1]/hooks")] // unique local fc00::/7
    [InlineData("https://[fd12:3456::1]/hooks")] // unique local fd
    [InlineData("https://[fe80::1]/hooks")] // link-local
    [InlineData("https://[ff02::1]/hooks")] // multicast
    [InlineData("https://[::ffff:10.0.0.1]/hooks")] // IPv4-mapped private
    [InlineData("https://[64:ff9b::a00:1]/hooks")] // NAT64-embedded 10.0.0.1
    [Requirement("CC-API-009")]
    public void Blocked_or_malformed_receiver_urls_are_rejected(string url)
    {
        var resolver = new FakeWebhookAddressResolver();
        resolver.ByHost["localhost"] = [IPAddress.Loopback];

        Assert.Throws<WebhookUrlRejectedException>(
            () => WebhookUrlValidator.EnsureDeliverable(new Uri(url), resolver));
    }

    [Fact]
    [Requirement("CC-API-009")]
    public void A_public_https_receiver_is_accepted_by_name_and_by_literal()
    {
        WebhookUrlValidator.EnsureDeliverable(
            new Uri("https://hooks.partner.example.com/orders"), PublicResolver);
        WebhookUrlValidator.EnsureDeliverable(
            new Uri("https://93.184.216.34/orders"), PublicResolver);
        WebhookUrlValidator.EnsureDeliverable(
            new Uri("https://[2606:2800:220:1:248:1893:25c8:1946]/orders"), PublicResolver);
    }

    [Fact]
    [Requirement("CC-API-009")]
    public void A_public_name_resolving_to_a_private_address_is_rejected()
    {
        var resolver = new FakeWebhookAddressResolver();
        resolver.ByHost["innocent-looking.example.com"] = [IPAddress.Parse("93.184.216.34"), IPAddress.Parse("10.0.0.5")];

        // ANY resolved address being private rejects (DNS-rebinding-conscious:
        // multi-answer names cannot smuggle one private target).
        Assert.Throws<WebhookUrlRejectedException>(() => WebhookUrlValidator.EnsureDeliverable(
            new Uri("https://innocent-looking.example.com/hooks"), resolver));
    }

    [Fact]
    [Requirement("CC-API-009")]
    public void Resolution_failure_fails_closed()
    {
        var resolver = new FakeWebhookAddressResolver { Throw = true };
        Assert.Throws<WebhookUrlRejectedException>(() => WebhookUrlValidator.EnsureDeliverable(
            new Uri("https://unresolvable.example.com/hooks"), resolver));

        var empty = new FakeWebhookAddressResolver();
        empty.ByHost["empty.example.com"] = [];
        Assert.Throws<WebhookUrlRejectedException>(() => WebhookUrlValidator.EnsureDeliverable(
            new Uri("https://empty.example.com/hooks"), empty));
    }

    [Fact]
    [Requirement("CC-API-009")]
    public void Registration_rejects_blocked_urls_before_storing_and_accepts_public_ones()
    {
        var resolver = new FakeWebhookAddressResolver();
        var registry = new InMemoryPartnerWebhookRegistry(resolver, new ManualTimeProvider(Fixtures.T0));
        var context = Fixtures.ApprovedContext("partner-a", Fixtures.DeIdentity());

        Assert.Throws<WebhookUrlRejectedException>(
            () => registry.Register(context, new Uri("https://169.254.169.254/hooks")));
        Assert.Null(registry.FindFor(context.PartnerId));

        var registration = registry.Register(context, new Uri("https://hooks.partner-a.example.com/orders"));
        Assert.Equal(context.PartnerId, registration.Owner);
        Assert.Equal(Fixtures.T0, registration.RegisteredAt);
        Assert.Same(registration, registry.FindFor(context.PartnerId));
    }

    [Fact]
    [Requirement("CC-API-009")]
    public void Rejection_messages_never_echo_the_submitted_url()
    {
        var exception = Assert.Throws<WebhookUrlRejectedException>(() => WebhookUrlValidator.EnsureDeliverable(
            new Uri("https://user:secret@partner.example.com/hooks"), PublicResolver));

        Assert.DoesNotContain("secret", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("partner.example.com", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
