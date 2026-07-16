using CacheCow.Modules.CatalogInventory.Catalog;
using CacheCow.Modules.WholesaleB2B.Auth;
using CacheCow.Modules.WholesaleB2B.Invoices;
using CacheCow.Modules.WholesaleB2B.Partners;
using CacheCow.Modules.WholesaleB2B.PriceLists;
using CacheCow.SharedKernel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CacheCow.Host.Tests.Composition;

/// <summary>
/// TEST-ONLY fixtures for the host-composition suite. The fakes stand in only
/// for adapters whose real implementations are later issues (the Entra client
/// directory, the Invoicing invoice-reader adapter); everything else in these
/// tests runs the REAL registered services — MarketGating, PricingPromotions,
/// OrderingPayments, BackOffice audit store — through the real host pipeline.
/// </summary>
internal static class B2BFixtures
{
    // Two fake partner tenants, per the IDOR harness convention.
    public const string TenantA = "tenant-a";
    public const string TenantB = "tenant-b";
    public const string ClientA = "client-a";
    public const string ClientB = "client-b";
    public const string ClientIn = "client-in";

    /// <summary>
    /// Builds an APPROVED tenant through the real onboarding workflow (the
    /// only path to Approved — no self-service activation exists, CC-WHS-002);
    /// the workflow audit goes to a local test sink because these fixtures
    /// exist before the host does.
    /// </summary>
    public static PartnerTenant ApprovedTenant(string partnerId, string legalName, params Market[] markets)
    {
        var tenant = PartnerTenant.Create(
            PartnerId.Parse(partnerId),
            legalName,
            markets.Select(market => BusinessIdentity.Create(market, IdentityValueFor(market))));

        var workflow = new PartnerOnboardingWorkflow(new RecordingPartnerAuditSink());
        var actor = DashboardActorProof.ForAuthenticatedStaff("test-staff", "admin");
        return workflow.Approve(workflow.Submit(tenant, actor), actor);
    }

    /// <summary>Schema-valid per-market business identifiers (CC-WHS-002).</summary>
    public static string IdentityValueFor(Market market) =>
        market == Market.DE ? "DE123456789"
        : market == Market.IN ? "27ABCDE1234F1Z5"
        : market.Code + "-TEST-BUSINESS-ID";

    /// <summary>A fully populated, valid catalog SKU with the classification under test (CC-CAT-001).</summary>
    public static Sku CatalogSku(string id, ProductClassification classification, params Market[] availableMarkets)
    {
        var enUs = Locale.Parse("en-US");
        var text = (string value) => LocalizedText.Create(new Dictionary<Locale, string> { [enUs] = value });
        var nutrition = NutritionFacts.Per100Grams(1050m, 251m, 18.5m, 7.2m, 1.1m, 0.9m, 21.3m, 1.8m);

        return Sku.Create(
            SkuId.Parse(id),
            text("Test " + id),
            classification,
            CutCategory.Parse("test-cut"),
            NetWeight.FromGrams(500),
            ServingEstimate.PerPackage(2),
            [new Ingredient(text("Test ingredient"))],
            new HashSet<Allergen>(),
            availableMarkets.ToDictionary(market => market, _ => nutrition),
            text("Keep frozen at -18 C."),
            text("Oven 20 minutes at 180 C."),
            availableMarkets);
    }

    /// <summary>Seeds the module's in-memory SKU catalog on the built host.</summary>
    public static void SeedCatalog(WebApplicationFactory<Program> factory, params Sku[] skus)
    {
        var catalog = Assert.IsType<InMemorySkuCatalog>(
            factory.Services.GetRequiredService<ISkuCatalog>());
        foreach (var sku in skus)
        {
            catalog.Add(sku);
        }
    }

    /// <summary>Registers a wholesale price list on the built host's in-memory store.</summary>
    public static void SeedPriceList(
        WebApplicationFactory<Program> factory,
        string partnerId,
        Market market,
        params (string Sku, int CasePack, long PricePerCaseMinorUnits)[] lines)
    {
        var currency = market == Market.US ? Currency.Usd
            : market == Market.MX ? Currency.Mxn
            : market == Market.JP ? Currency.Jpy
            : market == Market.IN ? Currency.Inr
            : Currency.Eur;
        factory.Services.GetRequiredService<InMemoryWholesalePriceLists>().Register(
            new WholesalePriceList(
                PartnerId.Parse(partnerId),
                market,
                lines.Select(line => new WholesalePriceLine(
                    SkuId.Parse(line.Sku),
                    line.CasePack,
                    Money.FromMinorUnits(line.PricePerCaseMinorUnits, currency)))));
    }

    /// <summary>An authenticated HTTPS client presenting B2B claims through the test scheme.</summary>
    public static HttpClient B2BClient(
        WebApplicationFactory<Program> factory,
        string clientId,
        string scopes,
        bool bearerOnly = false)
    {
        var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, clientId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.ClientIdHeader, clientId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopesHeader, scopes);
        if (bearerOnly)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.BearerOnlyHeader, "true");
        }

        return client;
    }

    /// <summary>Headers equivalent to <see cref="B2BClient"/>, for the IDOR harness routes.</summary>
    public static IReadOnlyDictionary<string, string> B2BHeaders(string clientId, string scopes) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [TestAuthHandler.ClientIdHeader] = clientId,
            [TestAuthHandler.ScopesHeader] = scopes,
        };
}

/// <summary>
/// TEST-ONLY stand-in for the Entra ID client-registration directory
/// (issues 054/058): maps OAuth2 client ids to approved partner tenants.
/// </summary>
internal sealed class FakeB2BClientDirectory : IB2BClientDirectory
{
    private readonly Dictionary<string, PartnerTenant> _byClientId;

    public FakeB2BClientDirectory(IReadOnlyDictionary<string, PartnerTenant> byClientId)
    {
        _byClientId = new Dictionary<string, PartnerTenant>(byClientId, StringComparer.Ordinal);
    }

    public PartnerTenant? FindByClientId(string clientId) =>
        _byClientId.GetValueOrDefault(clientId);
}

/// <summary>
/// TEST-ONLY stand-in for the Invoicing invoice-reader adapter (a later
/// issue), honoring the port's tenancy contract: an invoice resolves only
/// through its owning partner's context — "not found" and "not yours" are
/// both null (CC-API-004; SECURITY.md, Authentication rule 9).
/// </summary>
internal sealed class FakeWholesaleInvoiceReader : IWholesaleInvoiceReader
{
    private readonly Dictionary<string, (PartnerId Owner, WholesaleInvoiceSummary Invoice)> _invoices = new(StringComparer.Ordinal);

    public void Add(string ownerPartnerId, WholesaleInvoiceSummary invoice)
    {
        _invoices[invoice.InvoiceId] = (PartnerId.Parse(ownerPartnerId), invoice);
    }

    public WholesaleInvoiceSummary? FindInvoice(PartnerTenantContext context, string invoiceId) =>
        _invoices.TryGetValue(invoiceId, out var entry) && entry.Owner == context.PartnerId
            ? entry.Invoice
            : null;
}

/// <summary>TEST-ONLY audit sink for fixture-time tenant approval (retains events, never fails).</summary>
internal sealed class RecordingPartnerAuditSink : IPartnerAuditSink
{
    public List<PartnerAuditEvent> Events { get; } = [];

    public void Append(PartnerAuditEvent auditEvent) => Events.Add(auditEvent);
}
