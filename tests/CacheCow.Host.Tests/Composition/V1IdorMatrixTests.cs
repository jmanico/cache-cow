using CacheCow.Modules.WholesaleB2B.Auth;
using CacheCow.Modules.WholesaleB2B.Invoices;
using CacheCow.Modules.WholesaleB2B.Orders;
using CacheCow.Modules.WholesaleB2B.Partners;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CacheCow.Host.Tests.Composition;

/// <summary>
/// The REAL /v1 order and invoice routes plugged into the issue-062 IDOR
/// harness with two fake partner tenants (CC-QA-005, CC-API-004, CC-SEC-007;
/// SECURITY.md, Authentication rules 8-9): unauthenticated is 401, the wrong
/// tenant gets a 404 indistinguishable from a nonexistent resource with no
/// content or existence disclosure, and the rightful tenant reads its own
/// resource.
/// </summary>
[Requirement("CC-QA-005")]
[Requirement("CC-API-004")]
public sealed class V1IdorMatrixTests : IdorMatrixTestBase
{
    private static readonly WholesaleOrder TenantAOrder = WholesaleOrder.Create(
        PartnerId.Parse(B2BFixtures.TenantA),
        Market.DE,
        [new WholesaleOrderLine(SkuId.Parse("RIBS-04"), 2, Money.FromMinorUnits(80_000, Currency.Eur))],
        Money.FromMinorUnits(80_000, Currency.Eur),
        new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero));

    private const string TenantAInvoiceId = "winv-a-0001";

    protected override WebApplicationFactory<Program> CreateFactory()
    {
        var tenantA = B2BFixtures.ApprovedTenant(B2BFixtures.TenantA, "Partner A GmbH", Market.DE);
        var tenantB = B2BFixtures.ApprovedTenant(B2BFixtures.TenantB, "Partner B GmbH", Market.DE);

        var invoices = new FakeWholesaleInvoiceReader();
        invoices.Add(B2BFixtures.TenantA, new WholesaleInvoiceSummary(
            TenantAInvoiceId, TenantAOrder.Id, "EUR", 80_000, "issued"));

        return TestHostBuilder.Create(configureServices: services =>
        {
            services.AddSingleton<IB2BClientDirectory>(new FakeB2BClientDirectory(
                new Dictionary<string, PartnerTenant>
                {
                    [B2BFixtures.ClientA] = tenantA,
                    [B2BFixtures.ClientB] = tenantB,
                }));

            var orders = new InMemoryWholesaleOrders();
            orders.Add(TenantAOrder);
            services.AddSingleton(orders);

            services.AddSingleton<IWholesaleInvoiceReader>(invoices);
        });
    }

    protected override IReadOnlyList<ProtectedResourceRoute> Routes { get; } =
    [
        new(
            "wholesale order via /v1 (CC-API-004)",
            HttpMethod.Get,
            $"/v1/orders/{TenantAOrder.Id}",
            "/v1/orders/wo_does_not_exist",
            ExpectedBody: TenantAOrder.Id,
            SecretMarker: "80000",
            OwnerUser: B2BFixtures.ClientA,
            OwnerTenant: null,
            ForeignUser: B2BFixtures.ClientB,
            ForeignTenant: null,
            OwnerHeaders: B2BFixtures.B2BHeaders(B2BFixtures.ClientA, "orders:read"),
            ForeignHeaders: B2BFixtures.B2BHeaders(B2BFixtures.ClientB, "orders:read")),
        new(
            "wholesale invoice via /v1 (CC-API-004)",
            HttpMethod.Get,
            $"/v1/invoices/{TenantAInvoiceId}",
            "/v1/invoices/winv-does-not-exist",
            ExpectedBody: TenantAInvoiceId,
            SecretMarker: "80000",
            OwnerUser: B2BFixtures.ClientA,
            OwnerTenant: null,
            ForeignUser: B2BFixtures.ClientB,
            ForeignTenant: null,
            OwnerHeaders: B2BFixtures.B2BHeaders(B2BFixtures.ClientA, "invoices:read"),
            ForeignHeaders: B2BFixtures.B2BHeaders(B2BFixtures.ClientB, "invoices:read")),
    ];
}
