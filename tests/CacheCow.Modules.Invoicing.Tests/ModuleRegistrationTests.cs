using CacheCow.Modules.Invoicing.Access;
using CacheCow.Modules.Invoicing.Invoices;
using CacheCow.Modules.Invoicing.Numbering;
using CacheCow.Modules.Invoicing.Rendering;
using CacheCow.SharedKernel.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CacheCow.Modules.Invoicing.Tests;

public sealed class ModuleRegistrationTests
{
    [Fact]
    [Requirement("CC-INV-001")]
    public void Module_registers_the_issuance_and_authorization_services()
    {
        var services = new ServiceCollection().AddInvoicingModule();
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ILegalEntitySequence>());
        Assert.NotNull(provider.GetRequiredService<InvoiceIssuer>());
        Assert.NotNull(provider.GetRequiredService<AccountSessionInvoiceAuthorizer>());
    }

    [Fact]
    [Requirement("CC-INV-002")]
    public void Open_decision_ports_are_not_defaulted_by_the_module()
    {
        var services = new ServiceCollection().AddInvoicingModule();
        using var provider = services.BuildServiceProvider();

        // The PDF renderer choice (issue 048, Open Questions) and the
        // capability-token implementation (issue 042, Ordering & Payments)
        // are supplied by the host — this module never invents them
        // (CLAUDE.md: never resolve an open decision).
        Assert.Null(provider.GetService<IInvoicePdfRenderer>());
        Assert.Null(provider.GetService<IGuestOrderCapabilityTokenValidator>());
    }

    private sealed class StubValidator : IGuestOrderCapabilityTokenValidator
    {
        public GuestCapabilityTokenValidation Validate(string presentedToken) =>
            GuestCapabilityTokenValidation.Invalid();
    }

    [Fact]
    [Requirement("CC-ORD-010")]
    [Requirement("CC-INV-002")]
    public void Guest_authorizer_resolves_once_the_host_adapts_the_token_port()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGuestOrderCapabilityTokenValidator, StubValidator>();
        services.AddInvoicingModule();
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<GuestCapabilityTokenInvoiceAuthorizer>());
    }

    [Fact]
    [Requirement("CC-ORD-010")]
    public void Guest_authorizer_requires_the_host_adapted_token_port_at_first_use()
    {
        var services = new ServiceCollection().AddInvoicingModule();
        using var provider = services.BuildServiceProvider();

        // Without the issue-042 adapter the guest path cannot exist — it fails
        // at resolution (closed), while every other service stays available.
        Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<GuestCapabilityTokenInvoiceAuthorizer>());
    }

    [Fact]
    [Requirement("CC-INV-002")]
    public void Host_supplied_ports_are_honored_not_overridden()
    {
        var services = new ServiceCollection();
        var hostSupplied = new InMemoryLegalEntitySequence();
        services.AddSingleton<ILegalEntitySequence>(hostSupplied);
        services.AddInvoicingModule();
        using var provider = services.BuildServiceProvider();

        // TryAdd semantics: the module never displaces a host registration
        // (e.g. the future durable, gapless PostgreSQL allocator).
        Assert.Same(hostSupplied, provider.GetRequiredService<ILegalEntitySequence>());
    }
}
