using System.Net;
using CacheCow.Host.TestSupport;
using CacheCow.SharedKernel.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CacheCow.Host.Tests;

/// <summary>
/// Issue 019 (CC-API-008, CC-CNT-004; SECURITY.md, HTTP boundary rule 7):
/// 429 + Retry-After with an RFC 9457 body, per-client partitioning, stricter
/// named policy classes for authentication and order creation, and no state
/// change on a rejected request.
/// </summary>
public sealed class RateLimitingTests
{
    private static Dictionary<string, string?> TightLimits(int authPermits, int orderPermits) => new()
    {
        ["Security:RateLimiting:Authentication:PermitLimit"] = authPermits.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["Security:RateLimiting:Authentication:WindowSeconds"] = "60",
        ["Security:RateLimiting:OrderCreation:PermitLimit"] = orderPermits.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["Security:RateLimiting:OrderCreation:WindowSeconds"] = "60",
    };

    [Fact]
    [Requirement("CC-API-008")]
    public async Task Over_limit_requests_get_429_with_retry_after_and_problem_details()
    {
        using var factory = TestHostBuilder.Create(TightLimits(authPermits: 2, orderPermits: 60));
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "alice");
        var uri = new Uri("/__test/limited-auth", UriKind.Relative);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(uri, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(uri, TestContext.Current.CancellationToken)).StatusCode);

        var rejected = await client.GetAsync(uri, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);

        var body = await rejected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        // Generic body only - no internal limiter state (issue 019 AC-06).
        Assert.DoesNotContain("queue", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", body, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-API-008")]
    public async Task Rate_limits_partition_per_client()
    {
        using var factory = TestHostBuilder.Create(TightLimits(authPermits: 2, orderPermits: 60));
        var uri = new Uri("/__test/limited-auth", UriKind.Relative);

        using var alice = factory.CreateHttpsClient();
        alice.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "alice");
        using var bob = factory.CreateHttpsClient();
        bob.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "bob");

        await alice.GetAsync(uri, TestContext.Current.CancellationToken);
        await alice.GetAsync(uri, TestContext.Current.CancellationToken);
        var aliceRejected = await alice.GetAsync(uri, TestContext.Current.CancellationToken);
        var bobAllowed = await bob.GetAsync(uri, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, aliceRejected.StatusCode);
        Assert.Equal(HttpStatusCode.OK, bobAllowed.StatusCode);
    }

    [Fact]
    [Requirement("CC-API-008")]
    public async Task Stricter_order_creation_policy_enforces_independently_and_creates_no_state_on_429()
    {
        using var factory = TestHostBuilder.Create(TightLimits(authPermits: 60, orderPermits: 2));
        using var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "alice");
        var uri = new Uri("/__test/limited-order", UriKind.Relative);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(uri, null, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(uri, null, TestContext.Current.CancellationToken)).StatusCode);

        var rejected = await client.PostAsync(uri, null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);

        // The over-limit "order creation" never executed its handler: the
        // counter still shows exactly the two permitted requests (fail closed
        // on the money path, issue 019 AC-04).
        var counter = factory.Services.GetRequiredService<TestOnlyEndpoints.OrderCounter>();
        Assert.Equal(2, counter.Value);

        // The default policy is unaffected by the stricter class.
        var stillAllowed = await client.GetAsync(new Uri("/__test/anonymous", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, stillAllowed.StatusCode);
    }
}
