using CacheCow.Modules.Invoicing.Access;
using CacheCow.Modules.OrderingPayments.GuestAccess;
using CacheCow.Modules.OrderingPayments.Orders;
using CacheCow.SharedKernel.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CacheCow.Host.Tests.Composition;

/// <summary>
/// Host composition: a guest capability token minted by the OrderingPayments
/// GuestAccessTokenService validates through the Invoicing context's
/// IGuestOrderCapabilityTokenValidator port via the host adapter — one token
/// implementation, consumed across the module seam (CC-ORD-010, CC-SEC-017,
/// CC-INV-002; SECURITY.md, Authentication rule 14; ARCHITECTURE.md,
/// Dependency rule 9).
/// </summary>
public sealed class GuestTokenCompositionTests : IDisposable
{
    private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory;

    public GuestTokenCompositionTests()
    {
        // TEST-ONLY lifetime: CC-ORD-010 mandates expiry but ratifies no
        // duration (issue 042, Open Questions). The shipped host registers no
        // GuestAccessOptions, so this path fails closed in production until a
        // human ratifies the value; the suite supplies an explicit test value
        // to exercise the adapter.
        _factory = TestHostBuilder.Create(configureServices: services =>
            services.AddSingleton(new GuestAccessOptions(tokenLifetime: TimeSpan.FromHours(1))));
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    [Requirement("CC-ORD-010")]
    [Requirement("CC-SEC-017")]
    public void Token_minted_by_ordering_validates_through_the_invoicing_port()
    {
        var tokens = _factory.Services.GetRequiredService<GuestAccessTokenService>();
        var validator = _factory.Services.GetRequiredService<IGuestOrderCapabilityTokenValidator>();

        var orderId = OrderId.New();
        var token = tokens.Issue(orderId, GuestAccessPurpose.InvoiceDownload);

        var validation = validator.Validate(token.RevealSecret());

        Assert.True(validation.IsValid);
        Assert.NotNull(validation.BoundOrder);
        Assert.Equal(orderId.ToString(), validation.BoundOrder.Value.Value);
    }

    [Fact]
    [Requirement("CC-ORD-010")]
    public void Wrong_purpose_revoked_and_garbage_tokens_are_all_indistinguishably_invalid()
    {
        var tokens = _factory.Services.GetRequiredService<GuestAccessTokenService>();
        var validator = _factory.Services.GetRequiredService<IGuestOrderCapabilityTokenValidator>();

        // Single-purpose binding: an order-status token grants no invoice access.
        var statusToken = tokens.Issue(OrderId.New(), GuestAccessPurpose.OrderStatus);
        Assert.False(validator.Validate(statusToken.RevealSecret()).IsValid);

        // Server-side revocation is effective through the adapter.
        var revoked = tokens.Issue(OrderId.New(), GuestAccessPurpose.InvoiceDownload);
        tokens.Revoke(revoked.Digest);
        Assert.False(validator.Validate(revoked.RevealSecret()).IsValid);

        // Malformed and unknown presentations: same invalid verdict, no detail.
        Assert.False(validator.Validate("not-a-token").IsValid);
        Assert.False(validator.Validate(string.Empty).IsValid);
    }

    [Fact]
    [Requirement("CC-INV-002")]
    public void Invoicing_guest_authorizer_grants_only_the_bound_order()
    {
        var tokens = _factory.Services.GetRequiredService<GuestAccessTokenService>();
        var validator = _factory.Services.GetRequiredService<IGuestOrderCapabilityTokenValidator>();

        var boundOrder = OrderId.New();
        var otherOrder = OrderId.New();
        var token = tokens.Issue(boundOrder, GuestAccessPurpose.InvoiceDownload);

        // The port verdict is bound to exactly one order: presenting the same
        // valid token for a different order resolves to the bound order only,
        // so the Invoicing authorizer denies the mismatch (issue 042, AC-04).
        var validation = validator.Validate(token.RevealSecret());
        Assert.True(validation.IsValid);
        Assert.Equal(boundOrder.ToString(), validation.BoundOrder!.Value.Value);
        Assert.NotEqual(otherOrder.ToString(), validation.BoundOrder.Value.Value);
    }
}
