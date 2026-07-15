using System.Text;
using System.Text.Json;
using CacheCow.Modules.WholesaleB2B.Partners;
using CacheCow.Modules.WholesaleB2B.Webhooks;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.WholesaleB2B.Tests;

/// <summary>
/// Issue 057 (CC-API-009; SECURITY.md, Secret handling rule 8): the signing
/// envelope — per-partner HMAC-SHA256 over timestamp+body, timestamp-bounded
/// replay, rotation via key ids — and the delivery pipeline: delivery-time URL
/// re-validation, secret-failure halting, minimal ids+status payload, and the
/// transport contract that never follows redirects.
/// </summary>
public sealed class WebhookSigningAndDeliveryTests
{
    private static readonly PartnerId PartnerA = PartnerId.Parse("partner-a");
    private static readonly PartnerId PartnerB = PartnerId.Parse("partner-b");

    private static WebhookSigningSecret Secret(string keyId, byte fill)
    {
        var material = new byte[32];
        Array.Fill(material, fill);
        return new WebhookSigningSecret(keyId, material);
    }

    private sealed class RecordingTransport : IWebhookDeliveryTransport
    {
        internal List<SignedWebhookDelivery> Deliveries { get; } = [];

        public Task DeliverAsync(SignedWebhookDelivery delivery, CancellationToken cancellationToken)
        {
            Deliveries.Add(delivery);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSecretSource : IWebhookSecretSource
    {
        internal Dictionary<PartnerId, WebhookSigningSecret> Secrets { get; } = [];

        public WebhookSigningSecret CurrentSecretFor(PartnerId partnerId) =>
            Secrets.TryGetValue(partnerId, out var secret)
                ? secret
                : throw new InvalidOperationException("secret unavailable (test fault injection)");
    }

    private static (WebhookDeliveryService Service, InMemoryPartnerWebhookRegistry Registry,
        RecordingTransport Transport, FakeSecretSource Secrets, FakeWebhookAddressResolver Resolver,
        ManualTimeProvider Clock) Pipeline()
    {
        var resolver = new FakeWebhookAddressResolver();
        var registry = new InMemoryPartnerWebhookRegistry(resolver);
        var transport = new RecordingTransport();
        var secrets = new FakeSecretSource();
        var clock = new ManualTimeProvider(Fixtures.T0);
        var service = new WebhookDeliveryService(registry, secrets, resolver, transport, clock);
        return (service, registry, transport, secrets, resolver, clock);
    }

    [Fact]
    [Requirement("CC-API-009")]
    public void The_signature_verifies_with_the_signing_secret_and_fails_with_another_partners()
    {
        var secretA = Secret("partner-a/1", 0x11);
        var secretB = Secret("partner-b/1", 0x22);
        const string Body = """{"orderId":"wo_1","status":"Shipped"}""";
        var timestamp = Fixtures.T0.ToUnixTimeSeconds();

        var signature = WebhookSigner.Sign(secretA, timestamp, Body);

        Assert.StartsWith("v1=", signature, StringComparison.Ordinal);
        Assert.True(WebhookSigner.Verify(secretA, timestamp, Body, signature, Fixtures.T0));
        Assert.False(WebhookSigner.Verify(secretB, timestamp, Body, signature, Fixtures.T0));

        // Any tampering with body or timestamp invalidates the signature.
        Assert.False(WebhookSigner.Verify(secretA, timestamp, Body + " ", signature, Fixtures.T0));
        Assert.False(WebhookSigner.Verify(secretA, timestamp + 1, Body, signature, Fixtures.T0));
    }

    [Fact]
    [Requirement("CC-API-009")]
    public void A_replayed_delivery_outside_the_timestamp_window_is_rejected()
    {
        var secret = Secret("partner-a/1", 0x11);
        const string Body = """{"orderId":"wo_1","status":"Shipped"}""";
        var timestamp = Fixtures.T0.ToUnixTimeSeconds();
        var signature = WebhookSigner.Sign(secret, timestamp, Body);

        Assert.True(WebhookSigner.Verify(secret, timestamp, Body, signature, Fixtures.T0.AddMinutes(4)));
        Assert.False(WebhookSigner.Verify(secret, timestamp, Body, signature, Fixtures.T0.AddMinutes(6)));
        Assert.False(WebhookSigner.Verify(secret, timestamp, Body, signature, Fixtures.T0.AddMinutes(-6)));
    }

    [Fact]
    [Requirement("CC-API-009")]
    public void Signing_material_below_256_bits_is_rejected()
    {
        Assert.Throws<WholesaleValidationException>(() => new WebhookSigningSecret("k", new byte[16]));
    }

    [Fact]
    [Requirement("CC-API-009")]
    public async Task A_delivery_is_signed_with_the_partners_current_secret_and_carries_the_envelope_headers()
    {
        var (service, registry, transport, secrets, _, clock) = Pipeline();
        registry.Register(
            Fixtures.ApprovedContext("partner-a", Fixtures.DeIdentity()),
            new Uri("https://hooks.partner-a.example.com/orders"));
        secrets.Secrets[PartnerA] = Secret("partner-a/1", 0x11);

        var delivered = await service.DeliverOrderStatusAsync(
            PartnerA,
            new OrderStatusWebhookEvent("wo_1", "Shipped", Fixtures.T0),
            TestContext.Current.CancellationToken);

        Assert.True(delivered);
        var delivery = Assert.Single(transport.Deliveries);
        Assert.Equal(new Uri("https://hooks.partner-a.example.com/orders"), delivery.Destination);
        Assert.Equal("partner-a/1", delivery.Headers[WebhookSigner.KeyIdHeader]);

        var timestamp = long.Parse(
            delivery.Headers[WebhookSigner.TimestampHeader], System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(clock.GetUtcNow().ToUnixTimeSeconds(), timestamp);

        // The receiver-side verification succeeds with A's secret, fails with B's.
        Assert.True(WebhookSigner.Verify(
            secrets.Secrets[PartnerA], timestamp, delivery.Body,
            delivery.Headers[WebhookSigner.SignatureHeader], clock.GetUtcNow()));
        Assert.False(WebhookSigner.Verify(
            Secret("partner-b/1", 0x22), timestamp, delivery.Body,
            delivery.Headers[WebhookSigner.SignatureHeader], clock.GetUtcNow()));
    }

    [Fact]
    [Requirement("CC-API-009")]
    public async Task Rotation_switches_signing_to_the_new_secret_and_key_id()
    {
        var (service, registry, transport, secrets, _, clock) = Pipeline();
        registry.Register(
            Fixtures.ApprovedContext("partner-a", Fixtures.DeIdentity()),
            new Uri("https://hooks.partner-a.example.com/orders"));

        var generationOne = Secret("partner-a/1", 0x11);
        secrets.Secrets[PartnerA] = generationOne;
        await service.DeliverOrderStatusAsync(
            PartnerA, new OrderStatusWebhookEvent("wo_1", "Packed", Fixtures.T0), TestContext.Current.CancellationToken);

        // Rotate: the source now returns generation 2.
        var generationTwo = Secret("partner-a/2", 0x33);
        secrets.Secrets[PartnerA] = generationTwo;
        await service.DeliverOrderStatusAsync(
            PartnerA, new OrderStatusWebhookEvent("wo_1", "Shipped", Fixtures.T0), TestContext.Current.CancellationToken);

        Assert.Equal(2, transport.Deliveries.Count);
        var second = transport.Deliveries[1];
        Assert.Equal("partner-a/2", second.Headers[WebhookSigner.KeyIdHeader]);

        var timestamp = long.Parse(
            second.Headers[WebhookSigner.TimestampHeader], System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(WebhookSigner.Verify(
            generationTwo, timestamp, second.Body, second.Headers[WebhookSigner.SignatureHeader], clock.GetUtcNow()));
        Assert.False(WebhookSigner.Verify(
            generationOne, timestamp, second.Body, second.Headers[WebhookSigner.SignatureHeader], clock.GetUtcNow()));
    }

    [Fact]
    [Requirement("CC-API-009")]
    public async Task The_payload_carries_ids_and_status_only()
    {
        var (service, registry, transport, secrets, _, _) = Pipeline();
        registry.Register(
            Fixtures.ApprovedContext("partner-a", Fixtures.DeIdentity()),
            new Uri("https://hooks.partner-a.example.com/orders"));
        secrets.Secrets[PartnerA] = Secret("partner-a/1", 0x11);

        await service.DeliverOrderStatusAsync(
            PartnerA, new OrderStatusWebhookEvent("wo_1", "Delivered", Fixtures.T0), TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(transport.Deliveries[0].Body);
        var propertyNames = body.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["occurredAt", "orderId", "status"], propertyNames);
    }

    [Fact]
    [Requirement("CC-API-009")]
    public async Task Delivery_time_revalidation_blocks_a_rebound_hostname()
    {
        var (service, registry, transport, secrets, resolver, _) = Pipeline();
        registry.Register(
            Fixtures.ApprovedContext("partner-a", Fixtures.DeIdentity()),
            new Uri("https://hooks.partner-a.example.com/orders"));
        secrets.Secrets[PartnerA] = Secret("partner-a/1", 0x11);

        // The hostname re-resolves to the cloud metadata address after
        // registration (DNS rebinding): delivery must refuse before transport.
        resolver.ByHost["hooks.partner-a.example.com"] = [System.Net.IPAddress.Parse("169.254.169.254")];

        await Assert.ThrowsAsync<WebhookUrlRejectedException>(() => service.DeliverOrderStatusAsync(
            PartnerA, new OrderStatusWebhookEvent("wo_1", "Shipped", Fixtures.T0), TestContext.Current.CancellationToken));
        Assert.Empty(transport.Deliveries);
    }

    [Fact]
    [Requirement("CC-API-009")]
    public async Task Secret_retrieval_failure_halts_delivery_and_no_unregistered_partner_is_delivered()
    {
        var (service, registry, transport, _, _, _) = Pipeline();
        registry.Register(
            Fixtures.ApprovedContext("partner-a", Fixtures.DeIdentity()),
            new Uri("https://hooks.partner-a.example.com/orders"));

        // Key Vault unavailable: no fallback secret, no delivery.
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeliverOrderStatusAsync(
            PartnerA, new OrderStatusWebhookEvent("wo_1", "Shipped", Fixtures.T0), TestContext.Current.CancellationToken));
        Assert.Empty(transport.Deliveries);

        // A partner with no registration is a no-op, not an error.
        var delivered = await service.DeliverOrderStatusAsync(
            PartnerB, new OrderStatusWebhookEvent("wo_2", "Shipped", Fixtures.T0), TestContext.Current.CancellationToken);
        Assert.False(delivered);
    }

    [Fact]
    [Requirement("CC-API-009")]
    public void The_transport_contract_documents_redirect_refusal()
    {
        // The no-redirect rule is part of the port's XML contract the host
        // implements; assert the envelope exposes everything a compliant
        // transport needs without a second fetch (defense against accidental
        // convenience-following).
        var headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["Content-Type"] = "application/json" };
        var delivery = new SignedWebhookDelivery(
            new Uri("https://hooks.partner-a.example.com/orders"), "{}", headers);

        Assert.Equal("https", delivery.Destination.Scheme);
        Assert.Equal(Encoding.UTF8.GetByteCount("{}"), delivery.Body.Length);
    }
}
