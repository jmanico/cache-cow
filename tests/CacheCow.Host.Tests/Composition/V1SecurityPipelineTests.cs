using System.Net;
using CacheCow.Modules.CatalogInventory.Catalog;
using CacheCow.Modules.WholesaleB2B.Auth;
using CacheCow.Modules.WholesaleB2B.Partners;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CacheCow.Host.Tests.Composition;

/// <summary>
/// The mapped /v1 surface runs inside the full issue-016-022 security
/// pipeline: unauthenticated requests are challenged 401 by the
/// deny-by-default fallback policy, authenticated-and-scoped requests
/// succeed, missing scopes are refused, bearer-only tokens are ceilinged to
/// read-only (CC-API-003), and every response carries the mandated security
/// headers with wholesale responses never cacheable (CC-SEC-003, CC-MKT-009).
/// </summary>
public sealed class V1SecurityPipelineTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;

    public V1SecurityPipelineTests()
    {
        var tenant = B2BFixtures.ApprovedTenant("partner-de", "DE Grocery Partner GmbH", Market.DE);
        _factory = TestHostBuilder.Create(configureServices: services =>
            services.AddSingleton<IB2BClientDirectory>(new FakeB2BClientDirectory(
                new Dictionary<string, PartnerTenant> { [B2BFixtures.ClientA] = tenant })));

        B2BFixtures.SeedCatalog(_factory, B2BFixtures.CatalogSku("RIBS-02", ProductClassification.NonVegetarian, Market.DE));
        B2BFixtures.SeedPriceList(_factory, "partner-de", Market.DE, ("RIBS-02", 8, 40_000));
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    [Requirement("CC-API-001")]
    [Requirement("CC-API-002")]
    public async Task Unauthenticated_v1_request_is_challenged_401()
    {
        using var client = _factory.CreateHttpsClient();

        using var response = await client.GetAsync(new Uri("/v1/catalog/DE", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Requirement("CC-API-001")]
    [Requirement("CC-SEC-003")]
    public async Task Scoped_client_gets_200_with_security_headers_and_no_store()
    {
        using var client = B2BFixtures.B2BClient(_factory, B2BFixtures.ClientA, "catalog:read");

        using var response = await client.GetAsync(new Uri("/v1/catalog/DE", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Issue 016/017 headers apply to the module-mapped endpoints too.
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.True(response.Headers.Contains("Referrer-Policy"));

        // Wholesale responses are personalized per tenant: never cacheable
        // (CC-MKT-009; SECURITY.md, HTTP boundary rule 10).
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    [Requirement("CC-API-004")]
    public async Task Missing_scope_is_refused_without_leaking_data()
    {
        // Authenticated, valid client — but no catalog:read scope.
        using var client = B2BFixtures.B2BClient(_factory, B2BFixtures.ClientA, "orders:read");

        using var response = await client.GetAsync(new Uri("/v1/catalog/DE", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("RIBS-02", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Requirement("CC-API-003")]
    public async Task Bearer_only_token_is_ceilinged_to_read_only()
    {
        // orders:write granted but no sender-constraining cnf claim: the
        // effective scopes drop to read-only, so the write is refused.
        using var client = B2BFixtures.B2BClient(_factory, B2BFixtures.ClientA, "orders:write", bearerOnly: true);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/v1/orders", UriKind.Relative))
        {
            Content = new StringContent(
                /*lang=json,strict*/ """{"market":"DE","lines":[{"sku":"RIBS-02","cases":1}]}""",
                System.Text.Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    [Requirement("CC-API-002")]
    public async Task Unregistered_client_id_is_a_generic_401()
    {
        using var client = B2BFixtures.B2BClient(_factory, "client-unknown", "catalog:read");

        using var response = await client.GetAsync(new Uri("/v1/catalog/DE", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
