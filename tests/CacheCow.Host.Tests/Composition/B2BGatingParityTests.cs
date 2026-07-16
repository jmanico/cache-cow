using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CacheCow.Modules.CatalogInventory.Catalog;
using CacheCow.Modules.WholesaleB2B.Auth;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CacheCow.Host.Tests.Composition;

/// <summary>
/// THE CC-API-007 parity test: market gating applies to the B2B API
/// identically to the storefront — a partner authorized for the IN market
/// MUST NOT be able to see or order a non-veg SKU through any endpoint
/// (CC-MKT-003/004 through the REAL MarketGating service, adapted onto the
/// module's IB2BGatingCheck port by host composition; ARCHITECTURE.md,
/// Dependency rule 1). The wholesale price list is deliberately mis-seeded
/// with a non-veg SKU in IN so the test proves the gating service — not the
/// price list — is what stops it.
/// </summary>
public sealed class B2BGatingParityTests : IDisposable
{
    private const string NonVegSku = "BRISKET-01";
    private const string VegSku = "PANEER-01";

    private readonly WebApplicationFactory<Program> _factory;

    public B2BGatingParityTests()
    {
        var tenantIn = B2BFixtures.ApprovedTenant("partner-in", "IN Grocery Partner Pvt Ltd", Market.IN);
        var tenantDe = B2BFixtures.ApprovedTenant("partner-de", "DE Grocery Partner GmbH", Market.DE);

        _factory = TestHostBuilder.Create(configureServices: services =>
            services.AddSingleton<IB2BClientDirectory>(new FakeB2BClientDirectory(
                new Dictionary<string, CacheCow.Modules.WholesaleB2B.Partners.PartnerTenant>
                {
                    [B2BFixtures.ClientIn] = tenantIn,
                    [B2BFixtures.ClientA] = tenantDe,
                })));

        B2BFixtures.SeedCatalog(
            _factory,
            B2BFixtures.CatalogSku(NonVegSku, ProductClassification.NonVegetarian, Market.US, Market.DE),
            B2BFixtures.CatalogSku(VegSku, ProductClassification.Vegetarian, Market.All.ToArray()));

        // Mis-seeded on purpose: the non-veg SKU is on the IN price list, so
        // only the real gating service stands between it and an IN response.
        B2BFixtures.SeedPriceList(_factory, "partner-in", Market.IN, (NonVegSku, 12, 90_000), (VegSku, 12, 60_000));
        B2BFixtures.SeedPriceList(_factory, "partner-de", Market.DE, (NonVegSku, 12, 90_000), (VegSku, 12, 60_000));
    }

    public void Dispose() => _factory.Dispose();

    private static StringContent OrderBody(string market, string sku, int cases) =>
        new(
            JsonSerializer.Serialize(new { market, lines = new[] { new { sku, cases } } }),
            System.Text.Encoding.UTF8,
            "application/json");

    [Fact]
    [Requirement("CC-API-007")]
    [Requirement("CC-MKT-003")]
    [Requirement("CC-QA-003")]
    public async Task IN_client_cannot_order_a_nonveg_sku_through_the_real_gating_service()
    {
        using var client = B2BFixtures.B2BClient(_factory, B2BFixtures.ClientIn, "orders:write orders:read");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/v1/orders", UriKind.Relative))
        {
            Content = OrderBody("IN", NonVegSku, 2),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Rejected without state change, without enumerating the gated
        // catalog (issue 055 failure behavior).
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(NonVegSku, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("veg", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Requirement("CC-API-007")]
    public async Task IN_client_can_order_a_veg_sku_with_server_recomputed_money()
    {
        using var client = B2BFixtures.B2BClient(_factory, B2BFixtures.ClientIn, "orders:write");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/v1/orders", UriKind.Relative))
        {
            Content = OrderBody("IN", VegSku, 3),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("IN", order.GetProperty("market").GetString());
        Assert.Equal("INR", order.GetProperty("currency").GetString());

        // 3 cases x 60000 minor units, recomputed server-side (CC-PRC-005).
        Assert.Equal(180_000, order.GetProperty("totalMinorUnits").GetInt64());
    }

    [Fact]
    [Requirement("CC-API-007")]
    [Requirement("CC-MKT-003")]
    public async Task IN_catalog_response_excludes_the_nonveg_sku_server_side()
    {
        using var client = B2BFixtures.B2BClient(_factory, B2BFixtures.ClientIn, "catalog:read");

        using var response = await client.GetAsync(new Uri("/v1/catalog/IN", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(VegSku, body, StringComparison.Ordinal);
        Assert.DoesNotContain(NonVegSku, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Requirement("CC-MKT-004")]
    public async Task Nonveg_product_detail_in_IN_is_404_indistinguishable_from_nonexistent()
    {
        using var client = B2BFixtures.B2BClient(_factory, B2BFixtures.ClientIn, "catalog:read");

        using var gated = await client.GetAsync(new Uri($"/v1/catalog/IN/{NonVegSku}", UriKind.Relative), TestContext.Current.CancellationToken);
        using var missing = await client.GetAsync(new Uri("/v1/catalog/IN/NO-SUCH-SKU", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, gated.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        // Identical RFC 9457 shape apart from the per-request correlation ids
        // (SECURITY.md, Authentication rule 9; CC-MKT-004 semantics).
        Assert.Equal(
            ProblemShape(await missing.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)),
            ProblemShape(await gated.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
    }

    private static string ProblemShape(string problemJson)
    {
        using var document = JsonDocument.Parse(problemJson);
        var properties = document.RootElement.EnumerateObject()
            .Where(p => p.Name is not "correlationId" and not "traceId")
            .Select(p => $"{p.Name}={p.Value.GetRawText()}");
        return string.Join(";", properties);
    }

    [Fact]
    [Requirement("CC-API-007")]
    [Requirement("CC-MKT-007")]
    public async Task The_same_nonveg_sku_is_orderable_in_DE_proving_the_denial_is_market_gating()
    {
        using var client = B2BFixtures.B2BClient(_factory, B2BFixtures.ClientA, "orders:write");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/v1/orders", UriKind.Relative))
        {
            Content = OrderBody("DE", NonVegSku, 1),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
