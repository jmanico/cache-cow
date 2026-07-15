using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.WholesaleB2B.Tests;

/// <summary>
/// Issue 053 (CC-API-001/006/010): every route lives under /v1, request bodies
/// validate against the published schema semantics — unknown fields rejected
/// (never stripped), wrong types rejected, missing required members rejected —
/// and every failure is a generic RFC 9457 problem-details body.
/// </summary>
public sealed class B2BSchemaValidationTests
{
    private static HttpRequestMessage OrderRequest(string body, string? idempotencyKey = "idem-1")
    {
        var request = B2BApiTestHost.Request(HttpMethod.Post, "/v1/orders", B2BApiTestHost.ClientA, "orders:write");
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return request;
    }

    private static async Task AssertProblemDetails(HttpResponseMessage response, int expectedStatus)
    {
        Assert.Equal(expectedStatus, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(expectedStatus, document.RootElement.GetProperty("status").GetInt32());
        Assert.True(document.RootElement.TryGetProperty("title", out _));

        // Generic bodies only (SECURITY.md, Logging rule 1).
        var raw = document.RootElement.GetRawText();
        Assert.DoesNotContain("Exception", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Requirement("CC-API-001")]
    public async Task No_unversioned_route_exists()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();

        using var unversioned = B2BApiTestHost.Request(HttpMethod.Get, "/catalog/DE", B2BApiTestHost.ClientA, "catalog:read");
        using var response = await client.SendAsync(unversioned, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var versioned = B2BApiTestHost.Request(HttpMethod.Get, "/v1/catalog/DE", B2BApiTestHost.ClientA, "catalog:read");
        using var ok = await client.SendAsync(versioned, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    [Requirement("CC-API-006")]
    public async Task An_unknown_field_is_rejected_with_400_problem_details_not_stripped()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();

        using var request = OrderRequest(
            """{"market":"DE","lines":[{"sku":"SKU-BRISKET","cases":2}],"discountPercent":100}""");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemDetails(response, 400);
    }

    [Fact]
    [Requirement("CC-API-006")]
    public async Task A_wrong_typed_field_is_rejected_with_400()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();

        // Strict number handling: a quoted number is a type violation.
        using var request = OrderRequest(
            """{"market":"DE","lines":[{"sku":"SKU-BRISKET","cases":"2"}]}""");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemDetails(response, 400);
    }

    [Fact]
    [Requirement("CC-API-006")]
    public async Task A_missing_required_member_is_rejected_with_400()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();

        using var request = OrderRequest("""{"market":"DE"}""");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemDetails(response, 400);
    }

    [Fact]
    [Requirement("CC-API-006")]
    public async Task Malformed_json_is_rejected_with_400_problem_details()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();

        using var request = OrderRequest("""{"market": """);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemDetails(response, 400);
    }

    [Fact]
    [Requirement("CC-API-006")]
    public async Task An_unexpected_content_type_is_rejected_with_415()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();

        var request = B2BApiTestHost.Request(HttpMethod.Post, "/v1/orders", B2BApiTestHost.ClientA, "orders:write");
        request.Headers.Add("Idempotency-Key", "idem-415");
        request.Content = new StringContent("market=DE", Encoding.UTF8, "text/plain");
        using var owned = request;
        using var response = await client.SendAsync(owned, TestContext.Current.CancellationToken);

        await AssertProblemDetails(response, 415);
    }

    [Fact]
    [Requirement("CC-API-001")]
    [Requirement("CC-API-006")]
    public async Task A_valid_order_request_succeeds_under_v1()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();

        using var request = OrderRequest(
            """{"market":"DE","lines":[{"sku":"SKU-BRISKET","cases":2},{"sku":"SKU-PANEER","cases":1}]}""");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.StartsWith("/v1/orders/wo_", response.Headers.Location?.ToString(), StringComparison.Ordinal);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("DE", body.RootElement.GetProperty("market").GetString());
        Assert.Equal("Received", body.RootElement.GetProperty("status").GetString());

        // Server-recomputed money (CC-PRC-005): 2 × 29 988 + 1 × 19 992.
        Assert.Equal(79_968, body.RootElement.GetProperty("totalMinorUnits").GetInt64());
    }

    [Fact]
    [Requirement("CC-API-006")]
    public async Task Rejected_bodies_are_not_processed()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();

        using var bad = OrderRequest(
            """{"market":"DE","lines":[{"sku":"SKU-BRISKET","cases":2}],"unknown":true}""", "idem-noproc");
        using var badResponse = await client.SendAsync(bad, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, badResponse.StatusCode);

        // The same key with a valid body succeeds: the rejected request never
        // claimed the key and never created state.
        using var good = OrderRequest(
            """{"market":"DE","lines":[{"sku":"SKU-BRISKET","cases":2}]}""", "idem-noproc");
        using var goodResponse = await client.SendAsync(good, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, goodResponse.StatusCode);
    }

    [Fact]
    public async Task Responses_are_json_and_never_cacheable()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();

        using var request = B2BApiTestHost.Request(HttpMethod.Get, "/v1/catalog/DE", B2BApiTestHost.ClientA, "catalog:read");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        _ = await response.Content.ReadFromJsonAsync<JsonDocument>(TestContext.Current.CancellationToken);
    }
}
