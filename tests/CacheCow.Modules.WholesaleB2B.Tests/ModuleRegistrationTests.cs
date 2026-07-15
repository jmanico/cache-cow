using CacheCow.Modules.WholesaleB2B.Partners;
using CacheCow.Modules.WholesaleB2B.PriceLists;
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
}
