using System.Net;
using System.Text;
using System.Text.Json;
using CacheCow.Modules.WholesaleB2B.Idempotency;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.WholesaleB2B.Tests;

/// <summary>
/// Issue 053/055 idempotency clauses (CC-API-005, CC-ORD-005 semantics,
/// CC-SEC-015): the Idempotency-Key header is mandatory on order creation,
/// replays return the original result without duplicate state, fingerprint
/// mismatches are 409, and keys are scoped per client so partners cannot
/// collide with — or replay — each other's keys.
/// </summary>
public sealed class B2BIdempotencyTests
{
    private const string ValidBody = """{"market":"DE","lines":[{"sku":"SKU-BRISKET","cases":2}]}""";

    private static HttpRequestMessage Order(string client, string body, string? key)
    {
        var request = B2BApiTestHost.Request(HttpMethod.Post, "/v1/orders", client, "orders:write");
        if (key is not null)
        {
            request.Headers.Add("Idempotency-Key", key);
        }

        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return request;
    }

    [Fact]
    [Requirement("CC-API-005")]
    public async Task A_missing_Idempotency_Key_header_is_a_400_problem()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();

        using var request = Order(B2BApiTestHost.ClientA, ValidBody, key: null);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    [Requirement("CC-API-005")]
    public async Task A_blank_Idempotency_Key_is_rejected()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();

        using var request = Order(B2BApiTestHost.ClientA, ValidBody, key: "   ");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    [Requirement("CC-API-005")]
    public async Task A_replay_with_the_same_key_and_body_returns_the_original_order_without_duplication()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();

        using var first = Order(B2BApiTestHost.ClientA, ValidBody, "idem-replay");
        using var firstResponse = await client.SendAsync(first, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        using var firstBody = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var firstOrderId = firstBody.RootElement.GetProperty("orderId").GetString();

        using var second = Order(B2BApiTestHost.ClientA, ValidBody, "idem-replay");
        using var secondResponse = await client.SendAsync(second, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        using var secondBody = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(firstOrderId, secondBody.RootElement.GetProperty("orderId").GetString());
    }

    [Fact]
    [Requirement("CC-API-005")]
    [Requirement("CC-SEC-015")]
    public async Task The_same_key_with_a_different_body_is_409_never_the_original_and_never_a_new_order()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();

        using var first = Order(B2BApiTestHost.ClientA, ValidBody, "idem-conflict");
        using var firstResponse = await client.SendAsync(first, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var mutated = Order(
            B2BApiTestHost.ClientA,
            """{"market":"DE","lines":[{"sku":"SKU-BRISKET","cases":200}]}""",
            "idem-conflict");
        using var mutatedResponse = await client.SendAsync(mutated, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, mutatedResponse.StatusCode);
        Assert.Equal("application/problem+json", mutatedResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    [Requirement("CC-SEC-015")]
    public async Task Keys_are_scoped_per_client_so_partners_never_collide()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();

        using var a = Order(B2BApiTestHost.ClientA, ValidBody, "idem-shared-key");
        using var aResponse = await client.SendAsync(a, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, aResponse.StatusCode);
        using var aBody = JsonDocument.Parse(await aResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        // Partner B reuses the identical key with a different body: neither a
        // conflict with A's entry nor a replay of A's order — a fresh order in
        // B's own scope.
        using var b = Order(
            B2BApiTestHost.ClientB,
            """{"market":"DE","lines":[{"sku":"SKU-RIBS","cases":1}]}""",
            "idem-shared-key");
        using var bResponse = await client.SendAsync(b, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, bResponse.StatusCode);
        using var bBody = JsonDocument.Parse(await bResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.NotEqual(
            aBody.RootElement.GetProperty("orderId").GetString(),
            bBody.RootElement.GetProperty("orderId").GetString());
    }

    [Fact]
    [Requirement("CC-SEC-015")]
    public void The_in_memory_store_honors_the_claim_contract()
    {
        var store = new InMemoryB2BOrderIdempotency();

        Assert.Equal(B2BIdempotencyStatus.Accepted, store.Claim("client-a", "k1", "fp1").Status);

        // In-flight duplicate fails closed (documented in-memory narrowing).
        Assert.Equal(B2BIdempotencyStatus.FingerprintConflict, store.Claim("client-a", "k1", "fp1").Status);

        store.Complete("client-a", "k1", "fp1", "wo_1");
        var replay = store.Claim("client-a", "k1", "fp1");
        Assert.Equal(B2BIdempotencyStatus.Replay, replay.Status);
        Assert.Equal("wo_1", replay.StoredOrderId);

        Assert.Equal(B2BIdempotencyStatus.FingerprintConflict, store.Claim("client-a", "k1", "fp2").Status);

        // Same key, different client scope: fresh.
        Assert.Equal(B2BIdempotencyStatus.Accepted, store.Claim("client-b", "k1", "fp1").Status);

        // Release frees an uncompleted reservation only.
        Assert.Equal(B2BIdempotencyStatus.Accepted, store.Claim("client-c", "k2", "fp1").Status);
        store.Release("client-c", "k2");
        Assert.Equal(B2BIdempotencyStatus.Accepted, store.Claim("client-c", "k2", "fp1").Status);
        store.Complete("client-c", "k2", "fp1", "wo_2");
        store.Release("client-c", "k2");
        Assert.Equal(B2BIdempotencyStatus.Replay, store.Claim("client-c", "k2", "fp1").Status);
    }
}
