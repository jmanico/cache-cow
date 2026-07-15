using System.Net;
using System.Text;
using System.Text.Json;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.WholesaleB2B.Tests;

/// <summary>
/// Issue 055 (CC-API-007): market gating applies to the B2B API identically to
/// the storefront — an IN-market partner cannot see or order non-veg SKUs
/// through ANY endpoint (CC-MKT-003 parity), gated SKU reads present as 404
/// (CC-MKT-004), and a gating-port fault denies rather than bypasses
/// (SECURITY.md, Logging rule 2). Enforcement flows through the IB2BGatingCheck
/// port to the Market &amp; Gating Policy context — the module has no market
/// conditional of its own (CC-MKT-006).
/// </summary>
public sealed class B2BInMarketGatingParityTests
{
    private static HttpRequestMessage InOrder(string body, string key)
    {
        var request = B2BApiTestHost.Request(HttpMethod.Post, "/v1/orders", B2BApiTestHost.ClientIn, "orders:write orders:read");
        request.Headers.Add("Idempotency-Key", key);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return request;
    }

    [Fact]
    [Requirement("CC-API-007")]
    [Requirement("CC-MKT-003")]
    public async Task The_IN_catalog_listing_excludes_non_veg_SKUs_server_side()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();

        using var request = B2BApiTestHost.Request(HttpMethod.Get, "/v1/catalog/IN", B2BApiTestHost.ClientIn, "catalog:read");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // The seeded IN price list contains SKU-BRISKET; the gating port must
        // have removed it before serialization (CC-MKT-003: server-side
        // exclusion, client-side hiding is non-compliant).
        Assert.DoesNotContain("SKU-BRISKET", payload, StringComparison.Ordinal);

        using var body = JsonDocument.Parse(payload);
        var skus = body.RootElement.GetProperty("lines").EnumerateArray()
            .Select(line => line.GetProperty("sku").GetString()!)
            .ToArray();
        Assert.Equal(["SKU-JACKFRUIT", "SKU-PANEER"], skus);
    }

    [Fact]
    [Requirement("CC-API-007")]
    [Requirement("CC-MKT-004")]
    public async Task A_direct_non_veg_SKU_read_in_IN_returns_404_not_403()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();

        using var gated = B2BApiTestHost.Request(
            HttpMethod.Get, "/v1/catalog/IN/SKU-BRISKET", B2BApiTestHost.ClientIn, "catalog:read");
        using var gatedResponse = await client.SendAsync(gated, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, gatedResponse.StatusCode);

        // Indistinguishable from a SKU that does not exist at all.
        using var missing = B2BApiTestHost.Request(
            HttpMethod.Get, "/v1/catalog/IN/SKU-NONEXISTENT", B2BApiTestHost.ClientIn, "catalog:read");
        using var missingResponse = await client.SendAsync(missing, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        Assert.Equal(
            await missingResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            await gatedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        // The same SKU is readable where it is not gated (DE partner).
        using var permitted = B2BApiTestHost.Request(
            HttpMethod.Get, "/v1/catalog/DE/SKU-BRISKET", B2BApiTestHost.ClientA, "catalog:read");
        using var permittedResponse = await client.SendAsync(permitted, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, permittedResponse.StatusCode);
    }

    [Fact]
    [Requirement("CC-API-007")]
    [Requirement("CC-MKT-003")]
    public async Task An_IN_order_containing_a_non_veg_SKU_is_rejected_with_no_state_change()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();

        using var request = InOrder(
            """{"market":"IN","lines":[{"sku":"SKU-PANEER","cases":1},{"sku":"SKU-BRISKET","cases":1}]}""",
            "idem-in-nonveg");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // No partial order, and the error does not enumerate the gated catalog.
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("SKU-BRISKET", body, StringComparison.Ordinal);
        Assert.DoesNotContain("veg", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Requirement("CC-API-007")]
    public async Task An_IN_order_of_vegetarian_SKUs_succeeds()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();

        using var request = InOrder(
            """{"market":"IN","lines":[{"sku":"SKU-PANEER","cases":2},{"sku":"SKU-JACKFRUIT","cases":1}]}""",
            "idem-in-veg");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("IN", body.RootElement.GetProperty("market").GetString());
        Assert.Equal("INR", body.RootElement.GetProperty("currency").GetString());
        Assert.Equal(2 * 99_900 + 89_900, body.RootElement.GetProperty("totalMinorUnits").GetInt64());
    }

    [Fact]
    [Requirement("CC-API-007")]
    [Requirement("CC-QA-005")]
    public async Task A_gating_port_fault_fails_closed_on_order_creation()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();
        host.Gating.Throw = true;

        using var request = InOrder(
            """{"market":"IN","lines":[{"sku":"SKU-PANEER","cases":1}]}""", "idem-in-fault");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Even an all-veg order is denied while gating cannot answer: an
        // exception in a gating path is a denial, never a bypass.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // And the key is reusable once gating recovers (no state was claimed).
        host.Gating.Throw = false;
        using var retry = InOrder(
            """{"market":"IN","lines":[{"sku":"SKU-PANEER","cases":1}]}""", "idem-in-fault");
        using var retryResponse = await client.SendAsync(retry, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, retryResponse.StatusCode);
    }

    [Fact]
    [Requirement("CC-API-007")]
    public async Task A_gating_port_fault_fails_closed_on_catalog_reads()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();
        host.Gating.Throw = true;

        using var listing = B2BApiTestHost.Request(HttpMethod.Get, "/v1/catalog/IN", B2BApiTestHost.ClientIn, "catalog:read");
        using var listingResponse = await client.SendAsync(listing, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, listingResponse.StatusCode);

        using var item = B2BApiTestHost.Request(
            HttpMethod.Get, "/v1/catalog/IN/SKU-PANEER", B2BApiTestHost.ClientIn, "catalog:read");
        using var itemResponse = await client.SendAsync(item, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, itemResponse.StatusCode);
    }
}
