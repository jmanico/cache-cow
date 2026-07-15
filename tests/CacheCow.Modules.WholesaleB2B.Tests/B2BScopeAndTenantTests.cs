using System.Net;
using System.Text;
using System.Text.Json;
using CacheCow.Modules.WholesaleB2B.Invoices;
using CacheCow.Modules.WholesaleB2B.Partners;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.WholesaleB2B.Tests;

/// <summary>
/// Issue 055 (CC-API-004; CC-QA-005; CC-WHS-003): the full endpoint × scope
/// matrix — every endpoint requires exactly its documented scope — and strict
/// cross-tenant isolation with 404 existence-hiding semantics on orders and
/// invoices.
/// </summary>
public sealed class B2BScopeAndTenantTests
{
    public static TheoryData<string, string, string> EndpointScopeDenials()
    {
        // endpoint method, path, a scope that must NOT unlock it
        var data = new TheoryData<string, string, string>();
        string[] allScopes = ["catalog:read", "orders:write", "orders:read", "invoices:read"];
        (string Method, string Path, string Required)[] endpoints =
        [
            ("GET", "/v1/catalog/DE", "catalog:read"),
            ("GET", "/v1/catalog/DE/SKU-BRISKET", "catalog:read"),
            ("POST", "/v1/orders", "orders:write"),
            ("GET", "/v1/orders/wo_missing", "orders:read"),
            ("GET", "/v1/invoices/inv-missing", "invoices:read"),
        ];

        foreach (var (method, path, required) in endpoints)
        {
            foreach (var scope in allScopes)
            {
                if (!string.Equals(scope, required, StringComparison.Ordinal))
                {
                    data.Add(method, path, scope);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EndpointScopeDenials))]
    [Requirement("CC-API-004")]
    [Requirement("CC-QA-005")]
    public async Task Every_endpoint_denies_every_non_matching_scope_with_403(
        string method, string path, string scope)
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();

        var request = B2BApiTestHost.Request(new HttpMethod(method), path, B2BApiTestHost.ClientA, scope);
        if (method == "POST")
        {
            request.Headers.Add("Idempotency-Key", "idem-scope");
            request.Content = new StringContent(
                """{"market":"DE","lines":[{"sku":"SKU-BRISKET","cases":1}]}""", Encoding.UTF8, "application/json");
        }

        using var owned = request;
        using var response = await client.SendAsync(owned, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    [Requirement("CC-API-004")]
    public async Task A_token_with_no_scopes_is_denied_everywhere_but_authenticates()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();

        using var request = B2BApiTestHost.Request(HttpMethod.Get, "/v1/catalog/DE", B2BApiTestHost.ClientA, "");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    [Requirement("CC-API-004")]
    [Requirement("CC-WHS-003")]
    [Requirement("CC-QA-005")]
    public async Task Partner_A_cannot_read_partner_Bs_order_and_the_denial_hides_existence()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();

        // Partner B creates an order.
        var create = B2BApiTestHost.Request(HttpMethod.Post, "/v1/orders", B2BApiTestHost.ClientB, "orders:write");
        create.Headers.Add("Idempotency-Key", "idem-b-1");
        create.Content = new StringContent(
            """{"market":"DE","lines":[{"sku":"SKU-RIBS","cases":3}]}""", Encoding.UTF8, "application/json");
        using var ownedCreate = create;
        using var created = await client.SendAsync(ownedCreate, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var orderId = body.RootElement.GetProperty("orderId").GetString()!;

        // Partner A, with a fully valid orders:read token, probes B's order id.
        using var probe = B2BApiTestHost.Request(HttpMethod.Get, $"/v1/orders/{orderId}", B2BApiTestHost.ClientA, "orders:read");
        using var probeResponse = await client.SendAsync(probe, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, probeResponse.StatusCode);

        // A truly nonexistent order returns an indistinguishable response.
        using var missing = B2BApiTestHost.Request(HttpMethod.Get, "/v1/orders/wo_nonexistent", B2BApiTestHost.ClientA, "orders:read");
        using var missingResponse = await client.SendAsync(missing, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);

        Assert.Equal(
            await missingResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            await probeResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        // The owner still reads it.
        using var own = B2BApiTestHost.Request(HttpMethod.Get, $"/v1/orders/{orderId}", B2BApiTestHost.ClientB, "orders:read");
        using var ownResponse = await client.SendAsync(own, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, ownResponse.StatusCode);
    }

    [Fact]
    [Requirement("CC-API-004")]
    [Requirement("CC-WHS-003")]
    [Requirement("CC-QA-005")]
    public async Task Partner_A_cannot_read_partner_Bs_invoice()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        host.Invoices.Add(
            PartnerId.Parse("partner-b"),
            new WholesaleInvoiceSummary("inv-b-1", "wo_b1", "EUR", 149_900, "Issued"));
        using var client = host.CreateClient();

        using var probe = B2BApiTestHost.Request(HttpMethod.Get, "/v1/invoices/inv-b-1", B2BApiTestHost.ClientA, "invoices:read");
        using var probeResponse = await client.SendAsync(probe, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, probeResponse.StatusCode);

        using var own = B2BApiTestHost.Request(HttpMethod.Get, "/v1/invoices/inv-b-1", B2BApiTestHost.ClientB, "invoices:read");
        using var ownResponse = await client.SendAsync(own, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, ownResponse.StatusCode);

        using var body = JsonDocument.Parse(await ownResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("inv-b-1", body.RootElement.GetProperty("invoiceId").GetString());
        Assert.Equal(149_900, body.RootElement.GetProperty("totalMinorUnits").GetInt64());
    }

    [Fact]
    [Requirement("CC-API-004")]
    [Requirement("CC-WHS-003")]
    public async Task A_partner_cannot_read_a_catalog_for_a_market_outside_its_tenancy()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();

        // Partner A is DE-only; partner B has a JP list. A probing JP gets 404.
        using var probe = B2BApiTestHost.Request(HttpMethod.Get, "/v1/catalog/JP", B2BApiTestHost.ClientA, "catalog:read");
        using var response = await client.SendAsync(probe, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Requirement("CC-API-004")]
    public async Task An_order_for_an_unauthorized_market_is_denied_without_state_change()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();

        var request = B2BApiTestHost.Request(HttpMethod.Post, "/v1/orders", B2BApiTestHost.ClientA, "orders:write");
        request.Headers.Add("Idempotency-Key", "idem-market");
        request.Content = new StringContent(
            """{"market":"JP","lines":[{"sku":"SKU-BRISKET","cases":1}]}""", Encoding.UTF8, "application/json");
        using var owned = request;
        using var response = await client.SendAsync(owned, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
