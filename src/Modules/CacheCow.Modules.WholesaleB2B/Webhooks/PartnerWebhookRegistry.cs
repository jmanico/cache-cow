using CacheCow.Modules.WholesaleB2B.Partners;

namespace CacheCow.Modules.WholesaleB2B.Webhooks;

/// <summary>A validated, registered webhook receiver endpoint for one partner.</summary>
public sealed class WebhookEndpointRegistration
{
    internal WebhookEndpointRegistration(PartnerId owner, Uri endpoint, DateTimeOffset registeredAt)
    {
        Owner = owner;
        Endpoint = endpoint;
        RegisteredAt = registeredAt;
    }

    public PartnerId Owner { get; }

    public Uri Endpoint { get; }

    public DateTimeOffset RegisteredAt { get; }
}

/// <summary>
/// Tenant-scoped webhook receiver registry (CC-API-009). Registration requires
/// the partner's own <see cref="PartnerTenantContext"/> — a partner registers
/// receivers only for itself — and every URL passes the full SSRF policy of
/// <see cref="WebhookUrlValidator"/> BEFORE it is stored, so no delivery is
/// ever attempted to an unvalidated destination (SECURITY.md, Input validation
/// rule 8). Whether registration is exposed self-service via the API, the
/// portal, or the dashboard is an open decision (issue 057, Open Questions):
/// this is deliberately a service seam, not an HTTP endpoint.
/// </summary>
public interface IPartnerWebhookRegistry
{
    /// <summary>Registers (or replaces) the partner's receiver after full URL validation.</summary>
    WebhookEndpointRegistration Register(PartnerTenantContext context, Uri receiverUrl);

    /// <summary>The partner's current registration, or null when none exists.</summary>
    WebhookEndpointRegistration? FindFor(PartnerId partnerId);
}

/// <summary>In-memory registry until the durable wholesale schema lands (issue 015).</summary>
public sealed class InMemoryPartnerWebhookRegistry : IPartnerWebhookRegistry
{
    private readonly Lock _gate = new();
    private readonly Dictionary<PartnerId, WebhookEndpointRegistration> _registrations = [];
    private readonly IWebhookAddressResolver _resolver;
    private readonly TimeProvider _timeProvider;

    public InMemoryPartnerWebhookRegistry(IWebhookAddressResolver resolver, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        _resolver = resolver;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public WebhookEndpointRegistration Register(PartnerTenantContext context, Uri receiverUrl)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(receiverUrl);

        // Reject before store: an invalid URL never persists (fail closed).
        WebhookUrlValidator.EnsureDeliverable(receiverUrl, _resolver);

        var registration = new WebhookEndpointRegistration(
            context.PartnerId, receiverUrl, _timeProvider.GetUtcNow());

        lock (_gate)
        {
            _registrations[context.PartnerId] = registration;
        }

        return registration;
    }

    public WebhookEndpointRegistration? FindFor(PartnerId partnerId)
    {
        lock (_gate)
        {
            return _registrations.TryGetValue(partnerId, out var registration) ? registration : null;
        }
    }
}
