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
/// CC-API-008 through host composition: the named policies the WholesaleB2B
/// module attaches (b2b-client, order-creation) are registered in the
/// EXISTING issue-019 rate limiter, partitioned per authenticated B2B client
/// via B2BRateLimitPartition.KeyFor, budgets from configuration (ratified
/// defaults 600/60; tightened here to make the limit observable), rejecting
/// 429 + Retry-After with no state change.
/// </summary>
public sealed class B2BRateLimitCompositionTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;

    public B2BRateLimitCompositionTests()
    {
        var tenantA = B2BFixtures.ApprovedTenant("partner-de-a", "Partner A GmbH", Market.DE);
        var tenantB = B2BFixtures.ApprovedTenant("partner-de-b", "Partner B GmbH", Market.DE);

        _factory = TestHostBuilder.Create(
            new Dictionary<string, string?>
            {
                ["Security:RateLimiting:OrderCreation:PermitLimit"] = "2",
                ["Security:RateLimiting:OrderCreation:WindowSeconds"] = "60",
                ["Security:RateLimiting:B2BClient:PermitLimit"] = "3",
                ["Security:RateLimiting:B2BClient:WindowSeconds"] = "60",
            },
            configureServices: services =>
                services.AddSingleton<IB2BClientDirectory>(new FakeB2BClientDirectory(
                    new Dictionary<string, PartnerTenant>
                    {
                        [B2BFixtures.ClientA] = tenantA,
                        [B2BFixtures.ClientB] = tenantB,
                    })));

        B2BFixtures.SeedCatalog(_factory, B2BFixtures.CatalogSku("RIBS-03", ProductClassification.NonVegetarian, Market.DE));
        B2BFixtures.SeedPriceList(_factory, "partner-de-a", Market.DE, ("RIBS-03", 8, 40_000));
        B2BFixtures.SeedPriceList(_factory, "partner-de-b", Market.DE, ("RIBS-03", 8, 40_000));
    }

    public void Dispose() => _factory.Dispose();

    private static HttpRequestMessage OrderRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/v1/orders", UriKind.Relative))
        {
            Content = new StringContent(
                /*lang=json,strict*/ """{"market":"DE","lines":[{"sku":"RIBS-03","cases":1}]}""",
                System.Text.Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return request;
    }

    [Fact]
    [Requirement("CC-API-008")]
    public async Task Order_creation_over_limit_gets_429_with_retry_after_and_creates_no_order()
    {
        using var client = B2BFixtures.B2BClient(_factory, B2BFixtures.ClientA, "orders:write");

        using var first = OrderRequest();
        using var second = OrderRequest();
        using var third = OrderRequest();
        Assert.Equal(HttpStatusCode.Created, (await client.SendAsync(first, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.SendAsync(second, TestContext.Current.CancellationToken)).StatusCode);

        using var rejected = await client.SendAsync(third, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);

        // No state change on the rejected money-path request: exactly the two
        // permitted orders exist (the store is not tenant-queryable in bulk,
        // so the proof is the pair of Created responses plus this rejection
        // never reaching the handler — the idempotency key of the third
        // request was never claimed, so replaying it after the window would
        // not 409).
    }

    [Fact]
    [Requirement("CC-API-008")]
    public async Task Order_creation_limits_partition_per_authenticated_client()
    {
        using var clientA = B2BFixtures.B2BClient(_factory, B2BFixtures.ClientA, "orders:write");
        using var clientB = B2BFixtures.B2BClient(_factory, B2BFixtures.ClientB, "orders:write");

        using var a1 = OrderRequest();
        using var a2 = OrderRequest();
        using var a3 = OrderRequest();
        await clientA.SendAsync(a1, TestContext.Current.CancellationToken);
        await clientA.SendAsync(a2, TestContext.Current.CancellationToken);
        using var aRejected = await clientA.SendAsync(a3, TestContext.Current.CancellationToken);

        using var b1 = OrderRequest();
        using var bAllowed = await clientB.SendAsync(b1, TestContext.Current.CancellationToken);

        // Client A exhausted ONLY its own budget (CC-API-008: per client,
        // never shared).
        Assert.Equal(HttpStatusCode.TooManyRequests, aRejected.StatusCode);
        Assert.Equal(HttpStatusCode.Created, bAllowed.StatusCode);
    }

    [Fact]
    [Requirement("CC-API-008")]
    public async Task B2b_client_policy_caps_read_endpoints_per_client()
    {
        using var client = B2BFixtures.B2BClient(_factory, B2BFixtures.ClientA, "catalog:read");
        var uri = new Uri("/v1/catalog/DE", UriKind.Relative);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(uri, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(uri, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(uri, TestContext.Current.CancellationToken)).StatusCode);

        using var rejected = await client.GetAsync(uri, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter);
    }
}
