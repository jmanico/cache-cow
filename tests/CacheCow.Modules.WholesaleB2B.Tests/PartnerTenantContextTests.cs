using System.Reflection;
using CacheCow.Modules.WholesaleB2B.Partners;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.WholesaleB2B.Tests;

/// <summary>
/// The tenant-scoped access context (CC-WHS-002 AC-01/AC-05, CC-WHS-003):
/// it exists only for approved tenants, so pending, rejected, and suspended
/// partners have no wholesale surface — fail closed, by construction.
/// </summary>
public sealed class PartnerTenantContextTests
{
    [Theory]
    [Requirement("CC-WHS-002")]
    [Requirement("CC-WHS-003")]
    [InlineData(PartnerOnboardingState.Draft)]
    [InlineData(PartnerOnboardingState.Submitted)]
    [InlineData(PartnerOnboardingState.Rejected)]
    [InlineData(PartnerOnboardingState.Suspended)]
    public void No_context_exists_for_a_non_approved_tenant(PartnerOnboardingState state)
    {
        var workflow = new PartnerOnboardingWorkflow(new RecordingPartnerAuditSink());
        var tenant = Fixtures.TenantIn(state, workflow);

        Assert.Throws<PartnerNotApprovedException>(() => PartnerTenantContext.ForApprovedTenant(tenant));
    }

    [Fact]
    [Requirement("CC-WHS-002")]
    [Requirement("CC-WHS-003")]
    public void An_approved_tenant_yields_a_context_scoped_to_its_own_tenancy()
    {
        var workflow = new PartnerOnboardingWorkflow(new RecordingPartnerAuditSink());
        var approved = Fixtures.TenantIn(
            PartnerOnboardingState.Approved, workflow, "partner-a", Fixtures.DeIdentity(), Fixtures.InIdentity());

        var context = PartnerTenantContext.ForApprovedTenant(approved);

        Assert.Equal(approved.Id, context.PartnerId);
        Assert.True(context.IsAuthorizedFor(Market.DE));
        Assert.True(context.IsAuthorizedFor(Market.IN));
        Assert.False(context.IsAuthorizedFor(Market.US));
        Assert.Equal(2, context.AuthorizedMarkets.Count);
    }

    [Fact]
    [Requirement("CC-WHS-003")]
    [Requirement("CC-QA-005")]
    public void The_context_cannot_be_minted_outside_the_approved_tenant_factory()
    {
        // The only construction path is ForApprovedTenant(PartnerTenant):
        // no public constructor, no factory taking raw identifiers a caller
        // could forge (SECURITY.md, Authentication rules 8-9).
        var contextType = typeof(PartnerTenantContext);

        Assert.Empty(contextType.GetConstructors(BindingFlags.Public | BindingFlags.Instance));

        var publicFactories = contextType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.ReturnType == contextType)
            .ToArray();

        var factory = Assert.Single(publicFactories);
        Assert.Equal(nameof(PartnerTenantContext.ForApprovedTenant), factory.Name);
        var parameter = Assert.Single(factory.GetParameters());
        Assert.Equal(typeof(PartnerTenant), parameter.ParameterType);
    }
}
