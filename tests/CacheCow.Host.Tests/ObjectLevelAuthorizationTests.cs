using System.Net;
using System.Security.Claims;
using CacheCow.Host.Security;
using CacheCow.SharedKernel.Testing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CacheCow.Host.Tests;

/// <summary>
/// Issue 062 (CC-SEC-007; SECURITY.md, Authentication rule 9): the
/// owner-or-tenant match of the resource-ownership handler, the 404-not-403
/// mapping, and fail-closed resource authorization under fault injection
/// (extends the issue 021 fault pattern to the resource path, CC-QA-005 AC-07).
/// </summary>
public sealed class ObjectLevelAuthorizationTests : IDisposable
{
    private sealed record OwnedThing(string OwnerId) : ITenantOwnedResource;

    private readonly WebApplicationFactory<Program> _factory = TestHostBuilder.Create();

    public void Dispose() => _factory.Dispose();

    private static async Task<AuthorizationHandlerContext> EvaluateAsync(ClaimsPrincipal user, string ownerId)
    {
        var context = new AuthorizationHandlerContext(
            [ResourceOwnershipRequirement.Instance], user, new OwnedThing(ownerId));
        await new ResourceOwnershipHandler().HandleAsync(context);
        return context;
    }

    private static ClaimsPrincipal PrincipalWith(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));

    [Fact]
    [Requirement("CC-SEC-007")]
    public async Task Tenant_claim_matching_the_resource_owner_succeeds()
    {
        var user = PrincipalWith(new Claim(ClaimTypes.Name, "buyer"), new Claim(CacheCowClaims.TenantId, "tenant-a"));

        var context = await EvaluateAsync(user, "tenant-a");

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    [Requirement("CC-SEC-007")]
    public async Task Consumer_identity_matching_the_resource_owner_succeeds()
    {
        var user = PrincipalWith(new Claim(ClaimTypes.NameIdentifier, "consumer-42"));

        var context = await EvaluateAsync(user, "consumer-42");

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    [Requirement("CC-SEC-007")]
    public async Task Cross_tenant_caller_hard_fails_the_requirement()
    {
        var user = PrincipalWith(new Claim(ClaimTypes.Name, "buyer"), new Claim(CacheCowClaims.TenantId, "tenant-b"));

        var context = await EvaluateAsync(user, "tenant-a");

        Assert.False(context.HasSucceeded);
        Assert.True(context.HasFailed, "ownership mismatch must be a hard Fail no other handler can override");
    }

    [Fact]
    [Requirement("CC-SEC-007")]
    public async Task Resource_without_an_owner_fails_closed()
    {
        var user = PrincipalWith(new Claim(ClaimTypes.Name, "anyone"), new Claim(CacheCowClaims.TenantId, "tenant-a"));

        var context = await EvaluateAsync(user, string.Empty);

        Assert.False(context.HasSucceeded);
        Assert.True(context.HasFailed);
    }

    [Fact]
    [Requirement("CC-SEC-007")]
    public async Task Anonymous_principal_never_matches_ownership()
    {
        var context = await EvaluateAsync(new ClaimsPrincipal(new ClaimsIdentity()), "tenant-a");

        Assert.False(context.HasSucceeded);
        Assert.True(context.HasFailed);
    }

    [Fact]
    [Requirement("CC-QA-005")]
    [Requirement("CC-SEC-007")]
    public async Task Exception_inside_resource_authorization_denies_with_404_never_the_resource()
    {
        using var client = _factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "alice");
        client.DefaultRequestHeaders.Add(TestAuthHandler.TenantHeader, "tenant-a");

        // The rightful owner, but the authorization handler throws: fail
        // closed to the same denial (SECURITY.md, Logging rule 2; AC-07).
        var response = await client.GetAsync(
            new Uri("/__test/resources/faulted/ord-a-1", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("order-secret-tenant-a", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("test fault injection", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Requirement("CC-SEC-007")]
    public async Task Role_denied_resource_endpoint_answers_404_not_403()
    {
        using var client = _factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "alice");
        client.DefaultRequestHeaders.Add(TestAuthHandler.TenantHeader, "tenant-a");
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "sales-viewer");

        var response = await client.GetAsync(
            new Uri("/__test/resources/ops-notes/note-a-1", UriKind.Relative), TestContext.Current.CancellationToken);

        // 404, never 403: existence of a role-gated resource is not
        // confirmed (SECURITY.md, Authentication rule 9; CC-MKT-004 posture).
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
