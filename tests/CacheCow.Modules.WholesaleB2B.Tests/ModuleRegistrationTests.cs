using CacheCow.Modules.WholesaleB2B.Auth;
using CacheCow.Modules.WholesaleB2B.Idempotency;
using CacheCow.Modules.WholesaleB2B.Orders;
using CacheCow.Modules.WholesaleB2B.Partners;
using CacheCow.Modules.WholesaleB2B.PriceLists;
using CacheCow.Modules.WholesaleB2B.RateLimits;
using CacheCow.Modules.WholesaleB2B.Webhooks;
using CacheCow.SharedKernel.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CacheCow.Modules.WholesaleB2B.Tests;

/// <summary>
/// Module registration for the Wholesale &amp; B2B bounded context (issues 049
/// and 050): the onboarding workflow requires the host-supplied append-only
/// audit sink — no sink, no workflow, no unaudited approval path — and the
/// price-list read port resolves to the single tenant-scoped store.
/// </summary>
public sealed class ModuleRegistrationTests
{
    [Fact]
    [Requirement("CC-WHS-002")]
    public void The_workflow_cannot_be_resolved_without_the_audit_sink()
    {
        var provider = new ServiceCollection()
            .AddWholesaleB2BModule()
            .BuildServiceProvider();

        // Fail closed: an onboarding workflow with no audit store is
        // unobtainable (SECURITY.md, Logging rules 2 and 6).
        Assert.Throws<InvalidOperationException>(provider.GetRequiredService<PartnerOnboardingWorkflow>);
    }

    [Fact]
    [Requirement("CC-WHS-002")]
    [Requirement("CC-WHS-003")]
    public void Registration_wires_the_workflow_and_the_tenant_scoped_price_list_port()
    {
        var provider = new ServiceCollection()
            .AddSingleton<IPartnerAuditSink>(new RecordingPartnerAuditSink())
            .AddWholesaleB2BModule()
            .BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<PartnerOnboardingWorkflow>());

        var port = provider.GetRequiredService<IWholesalePriceLists>();
        var store = provider.GetRequiredService<InMemoryWholesalePriceLists>();
        Assert.Same(store, port);
    }

    [Fact]
    [Requirement("CC-API-002")]
    public void The_token_validator_cannot_be_resolved_without_the_client_directory()
    {
        var provider = new ServiceCollection()
            .AddWholesaleB2BModule()
            .BuildServiceProvider();

        // Fail closed: no client-id → partner mapping, no B2B identity
        // (issue 054; SECURITY.md, Logging rule 2).
        Assert.Throws<InvalidOperationException>(provider.GetRequiredService<IB2BTokenClaimsValidator>);
    }

    [Fact]
    [Requirement("CC-API-005")]
    [Requirement("CC-API-008")]
    public void Registration_wires_the_b2b_api_services_behind_their_ports()
    {
        var provider = new ServiceCollection()
            .AddWholesaleB2BModule()
            .BuildServiceProvider();

        Assert.Same(
            provider.GetRequiredService<InMemoryWholesaleOrders>(),
            provider.GetRequiredService<IWholesaleOrders>());
        Assert.Same(
            provider.GetRequiredService<InMemoryB2BOrderIdempotency>(),
            provider.GetRequiredService<IB2BOrderIdempotency>());
        Assert.Same(
            provider.GetRequiredService<InMemoryB2BRateLimitTierSource>(),
            provider.GetRequiredService<IB2BRateLimitTierSource>());
    }

    [Fact]
    [Requirement("CC-API-009")]
    public void The_webhook_pipeline_cannot_be_resolved_without_the_host_ports()
    {
        var provider = new ServiceCollection()
            .AddWholesaleB2BModule()
            .BuildServiceProvider();

        // Fail closed: no DNS-resolution port means no SSRF validation, so no
        // registry; no Key Vault secret source / transport means no delivery
        // (issue 057; SECURITY.md, Input validation rule 8).
        Assert.Throws<InvalidOperationException>(provider.GetRequiredService<IPartnerWebhookRegistry>);
        Assert.Throws<InvalidOperationException>(provider.GetRequiredService<WebhookDeliveryService>);
    }
}
