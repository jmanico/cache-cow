using System.Text.Json;
using CacheCow.Modules.WholesaleB2B.Partners;

namespace CacheCow.Modules.WholesaleB2B.Webhooks;

/// <summary>
/// The order-status event payload (CC-API-009): identifiers and status only —
/// no customer or address PII, no money detail, no wholesale terms. The
/// receiving partner already owns the order; anything beyond ids and status is
/// fetched back through the authenticated, tenant-scoped API.
/// </summary>
public sealed record OrderStatusWebhookEvent
{
    public OrderStatusWebhookEvent(string orderId, string status, DateTimeOffset occurredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        OrderId = orderId;
        Status = status;
        OccurredAt = occurredAt;
    }

    public string OrderId { get; }

    public string Status { get; }

    public DateTimeOffset OccurredAt { get; }
}

/// <summary>A fully signed delivery: destination, exact body, and signature headers.</summary>
public sealed record SignedWebhookDelivery(
    Uri Destination,
    string Body,
    IReadOnlyDictionary<string, string> Headers);

/// <summary>
/// Outbound HTTP transport port for webhook delivery. The host implements it
/// over HttpClient. CONTRACT (SECURITY.md, Input validation rule 8; issue 057):
/// - HTTPS only; the destination has already passed
///   <see cref="WebhookUrlValidator"/> at registration and again immediately
///   before this call, and the transport MUST re-validate the address it
///   actually connects to (<c>AllowAutoRedirect = false</c>, resolved-address
///   check at connect time) so DNS rebinding between check and connect fails.
/// - MUST NOT follow redirects: any 3xx response is terminal for the attempt;
///   no further outbound request is made (a redirect is an unvalidated URL).
/// - MUST NOT log signing secrets, signature values, or receiver URLs with
///   embedded material (SECURITY.md, Logging rule 4).
/// - Retry/backoff and dead-lettering are an open decision (issue 057, Open
///   Questions): this port is a single attempt.
/// </summary>
public interface IWebhookDeliveryTransport
{
    Task DeliverAsync(SignedWebhookDelivery delivery, CancellationToken cancellationToken);
}

/// <summary>
/// Builds and signs order-status webhook deliveries (CC-API-009; SECURITY.md,
/// Secret handling rule 8): per-partner rotating HMAC secret from the Key
/// Vault-backed port, timestamp bound into the signature to limit replay,
/// delivery-time URL re-validation (DNS-rebinding conscious), and a minimal
/// ids-plus-status payload. Event scoping is the caller's tenancy duty: the
/// order's owning partner is the only partner this service will sign for.
/// </summary>
public sealed class WebhookDeliveryService
{
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    private readonly IPartnerWebhookRegistry _registry;
    private readonly IWebhookSecretSource _secrets;
    private readonly IWebhookAddressResolver _resolver;
    private readonly IWebhookDeliveryTransport _transport;
    private readonly TimeProvider _timeProvider;

    public WebhookDeliveryService(
        IPartnerWebhookRegistry registry,
        IWebhookSecretSource secrets,
        IWebhookAddressResolver resolver,
        IWebhookDeliveryTransport transport,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(secrets);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(transport);
        _registry = registry;
        _secrets = secrets;
        _resolver = resolver;
        _transport = transport;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Signs and hands one delivery to the transport. Returns false when the
    /// partner has no registered receiver. Throws (halting delivery, never
    /// falling back) when URL re-validation or secret retrieval fails.
    /// </summary>
    public async Task<bool> DeliverOrderStatusAsync(
        PartnerId partner,
        OrderStatusWebhookEvent orderEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orderEvent);

        var registration = _registry.FindFor(partner);
        if (registration is null)
        {
            return false;
        }

        // Delivery-time re-validation: registration-time checking alone is
        // DNS-rebindable (SECURITY.md, Input validation rule 8).
        WebhookUrlValidator.EnsureDeliverable(registration.Endpoint, _resolver);

        // Per-partner secret only; retrieval failure halts delivery for this
        // partner (issue 057, Failure Behavior) — exceptions propagate.
        var secret = _secrets.CurrentSecretFor(partner);

        var body = JsonSerializer.Serialize(orderEvent, PayloadOptions);
        var timestamp = _timeProvider.GetUtcNow().ToUnixTimeSeconds();

        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Content-Type"] = "application/json",
            [WebhookSigner.TimestampHeader] = timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [WebhookSigner.KeyIdHeader] = secret.KeyId,
            [WebhookSigner.SignatureHeader] = WebhookSigner.Sign(secret, timestamp, body),
        };

        await _transport.DeliverAsync(
            new SignedWebhookDelivery(registration.Endpoint, body, headers),
            cancellationToken);
        return true;
    }
}
