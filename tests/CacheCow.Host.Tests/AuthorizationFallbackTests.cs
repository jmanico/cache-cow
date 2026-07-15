using System.Net;
using CacheCow.SharedKernel.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CacheCow.Host.Tests;

/// <summary>
/// Issue 020 (CC-SEC-005/006/007, CC-DSH-001/002 supporting control;
/// SECURITY.md, Authentication rule 1): deny-by-default fallback policy, with
/// explicit opt-out only, and fail-closed authorization evaluation
/// (issue 021 AC-03; SECURITY.md, Logging rule 2).
/// </summary>
public sealed class AuthorizationFallbackTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory = TestHostBuilder.Create();

    public void Dispose() => _factory.Dispose();

    [Fact]
    [Requirement("CC-SEC-007")]
    public async Task Endpoint_without_authorization_metadata_denies_anonymous_requests()
    {
        using var client = _factory.CreateHttpsClient();

        var response = await client.GetAsync(new Uri("/__test/protected", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Requirement("CC-SEC-007")]
    public async Task Endpoint_without_authorization_metadata_serves_authenticated_requests()
    {
        using var client = _factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "alice");

        var response = await client.GetAsync(new Uri("/__test/protected", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("protected-ok", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [Requirement("CC-SEC-007")]
    public async Task Explicitly_anonymous_endpoint_serves_without_authentication()
    {
        using var client = _factory.CreateHttpsClient();

        var response = await client.GetAsync(new Uri("/__test/anonymous", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("anonymous-ok", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [Requirement("CC-SEC-007")]
    public async Task RequireAuthorization_endpoint_denies_anonymous_and_admits_authenticated()
    {
        using var anonymous = _factory.CreateHttpsClient();
        using var authenticated = _factory.CreateHttpsClient();
        authenticated.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "alice");
        var uri = new Uri("/__test/default-authz", UriKind.Relative);

        var denied = await anonymous.GetAsync(uri, TestContext.Current.CancellationToken);
        var admitted = await authenticated.GetAsync(uri, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Equal(HttpStatusCode.OK, admitted.StatusCode);
    }

    [Fact]
    [Requirement("CC-SEC-007")]
    [Requirement("CC-API-006")]
    public async Task Exception_in_authorization_evaluation_denies_instead_of_bypassing()
    {
        using var client = _factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "alice");

        var response = await client.GetAsync(new Uri("/__test/throwing-authz", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Fail closed: denial, not the protected content and not a 500 leaking
        // the evaluation fault (SECURITY.md, Logging rule 2).
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("never-served", body, StringComparison.Ordinal);
        Assert.DoesNotContain("authorization backend unavailable", body, StringComparison.Ordinal);
    }
}
