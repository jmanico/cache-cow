using System.Security.Claims;
using CacheCow.Modules.WholesaleB2B.Api;
using CacheCow.Modules.WholesaleB2B.Partners;
using CacheCow.Modules.WholesaleB2B.RateLimits;
using CacheCow.SharedKernel.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace CacheCow.Modules.WholesaleB2B.Tests;

/// <summary>
/// Issue 056 (CC-API-008): the per-client rate-limit CONTRACT the host's
/// limiter middleware (issue 019) plugs into — named policy metadata on every
/// /v1 endpoint (order creation carrying the stricter policy), a partition-key
/// provider keyed on the authenticated client identity (never IP for
/// authenticated traffic), and tier budgets as per-partner configuration with
/// the ratified 600/60 defaults. The 429 + Retry-After middleware behavior is
/// host scope and tested there.
/// </summary>
public sealed class B2BRateLimitContractTests
{
    private static IReadOnlyList<RouteEndpoint> MappedEndpoints(B2BApiTestHost host) =>
        [.. ((IEndpointRouteBuilder)host.App).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()];

    [Fact]
    [Requirement("CC-API-008")]
    public async Task Every_endpoint_carries_a_named_rate_limit_policy_and_order_creation_carries_the_stricter_one()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        var endpoints = MappedEndpoints(host);

        Assert.NotEmpty(endpoints);
        foreach (var endpoint in endpoints)
        {
            var policies = endpoint.Metadata.GetOrderedMetadata<EnableRateLimitingAttribute>();
            Assert.NotEmpty(policies);

            var isOrderCreate = endpoint.RoutePattern.RawText == "/v1/orders"
                && endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains("POST");

            // Closest-wins semantics: the last (endpoint-level) policy governs.
            var effective = policies[^1].PolicyName;
            Assert.Equal(
                isOrderCreate ? B2BRateLimitPolicies.OrderCreation : B2BRateLimitPolicies.Client,
                effective);
        }
    }

    [Fact]
    [Requirement("CC-API-008")]
    public async Task The_order_creation_policy_name_matches_the_hosts_existing_policy()
    {
        // The host registered "order-creation" in issue 019; the metadata must
        // bind to that exact name or the sublimit silently never applies.
        Assert.Equal("order-creation", B2BRateLimitPolicies.OrderCreation);
        Assert.Equal("b2b-client", B2BRateLimitPolicies.Client);
        await Task.CompletedTask;
    }

    [Fact]
    [Requirement("CC-API-008")]
    public void The_partition_key_is_the_authenticated_client_identity()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("client_id", "client-a")], "test"));

        Assert.Equal("b2b-client:client-a", B2BRateLimitPartition.KeyFor(principal));

        // Distinct clients partition separately (one partner cannot starve
        // another, issue 056 AC-04).
        var other = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("client_id", "client-b")], "test"));
        Assert.NotEqual(B2BRateLimitPartition.KeyFor(principal), B2BRateLimitPartition.KeyFor(other));
    }

    [Fact]
    [Requirement("CC-API-008")]
    public void Unauthenticated_principals_have_no_partner_partition()
    {
        // AC-07: invalid-token traffic must not consume any partner's quota.
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        Assert.Null(B2BRateLimitPartition.KeyFor(anonymous));

        var authenticatedWithoutClient = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "someone")], "test"));
        Assert.Null(B2BRateLimitPartition.KeyFor(authenticatedWithoutClient));
    }

    [Fact]
    [Requirement("CC-API-008")]
    public void Tier_budgets_default_to_the_ratified_values_and_override_per_partner_only()
    {
        var source = new InMemoryB2BRateLimitTierSource();
        var partnerA = PartnerId.Parse("partner-a");
        var partnerB = PartnerId.Parse("partner-b");

        Assert.Equal(600, source.TierFor(partnerA).RequestsPerMinute);
        Assert.Equal(60, source.TierFor(partnerA).OrderCreationsPerMinute);

        source.SetTier(partnerA, new B2BRateLimitTier(1200, 120));

        Assert.Equal(1200, source.TierFor(partnerA).RequestsPerMinute);
        Assert.Equal(120, source.TierFor(partnerA).OrderCreationsPerMinute);

        // Other partners are unaffected (issue 056, AC-03).
        Assert.Equal(600, source.TierFor(partnerB).RequestsPerMinute);
    }

    [Fact]
    [Requirement("CC-API-008")]
    public void Tier_budgets_must_be_positive()
    {
        Assert.Throws<WholesaleValidationException>(() => new B2BRateLimitTier(0, 60));
        Assert.Throws<WholesaleValidationException>(() => new B2BRateLimitTier(600, -1));
    }
}
