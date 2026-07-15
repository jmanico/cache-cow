using CacheCow.Modules.Invoicing.Access;
using CacheCow.Modules.Invoicing.Invoices;
using CacheCow.Modules.Invoicing.Numbering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CacheCow.Modules.Invoicing;

/// <summary>
/// Registration entry point for the Invoicing bounded context
/// (ARCHITECTURE.md, "Server bounded contexts" item 7): per-market legal
/// invoice generation, immutable with credit notes; server-rendered PDFs
/// behind authenticated download (CC-INV-001/002).
/// </summary>
public static class InvoicingModule
{
    public static IServiceCollection AddInvoicingModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Provisional (TryAdd so the host replaces it): durable gapless
        // allocation needs the PostgreSQL adapter, which is blocked on the
        // data-residency/write-region open decision (ARCHITECTURE.md,
        // "Known unknowns"; issue 046 AT RISK).
        services.TryAddSingleton<ILegalEntitySequence, InMemoryLegalEntitySequence>();

        services.TryAddSingleton<InvoiceIssuer>();

        // The two authorization contracts of CC-INV-002 (issue 048): guest
        // capability-token path and account object-level path. The guest
        // authorizer depends on IGuestOrderCapabilityTokenValidator, which the
        // HOST must adapt to the Ordering & Payments token implementation
        // (issue 042) — the token type is not duplicated here. Registered via
        // factory: the port is required at first use, not at container build,
        // so the host boots while the adapter issue lands (fail closed at the
        // guest path, not at startup of every other surface).
        services.TryAddSingleton(provider => new GuestCapabilityTokenInvoiceAuthorizer(
            provider.GetRequiredService<IGuestOrderCapabilityTokenValidator>()));
        services.TryAddSingleton<AccountSessionInvoiceAuthorizer>();

        // Deliberately NOT registered:
        // - IInvoicePdfRenderer: the rendering mechanism/library is an open
        //   decision (issue 048, Open Questions) subject to SECURITY.md
        //   Dependency Rules; the host supplies the adapter once decided.
        // - IGuestOrderCapabilityTokenValidator: adapted by the host from
        //   Ordering & Payments (issue 042).
        return services;
    }
}
