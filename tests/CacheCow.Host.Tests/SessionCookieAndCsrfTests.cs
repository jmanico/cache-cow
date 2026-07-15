using System.Net;
using CacheCow.Host.Security;
using CacheCow.SharedKernel.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CacheCow.Host.Tests;

/// <summary>
/// Issue 061 (CC-SEC-006; SECURITY.md, Authentication rule 11): session cookie
/// attributes (HttpOnly/Secure/SameSite/bounded Max-Age), server-side
/// revocation rejected at authentication time, fail-closed session validation,
/// and global antiforgery enforcement on cookie-authenticated state-changing
/// requests with the bearer-token exemption.
/// </summary>
public sealed class SessionCookieAndCsrfTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory = TestHostBuilder.Create();

    public void Dispose() => _factory.Dispose();

    private static Uri Url(string path) => new(path, UriKind.Relative);

    private async Task<(HttpClient Client, string SessionId)> SignInAsync(
        WebApplicationFactory<Program>? factory = null,
        string user = "alice",
        bool omitSessionId = false)
    {
        var client = (factory ?? _factory).CreateHttpsClient();
        var query = omitSessionId ? $"/__test/session/sign-in?user={user}&omitSessionId=true" : $"/__test/session/sign-in?user={user}";
        var response = await client.PostAsync(Url(query), null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sessionId = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (client, sessionId);
    }

    [Fact]
    [Requirement("CC-SEC-006")]
    public async Task Session_cookie_is_httponly_secure_samesite_with_bounded_expiry()
    {
        using var client = _factory.CreateHttpsClient();

        var response = await client.PostAsync(
            Url("/__test/session/sign-in?user=alice"), null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sessionCookie = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(".AspNetCore.TestCookie", StringComparison.OrdinalIgnoreCase));
        var attributes = sessionCookie.ToUpperInvariant();

        // Issue 061 AC-01: no session cookie without ALL of these.
        Assert.Contains("HTTPONLY", attributes, StringComparison.Ordinal);
        Assert.Contains("SECURE", attributes, StringComparison.Ordinal);
        Assert.Contains("SAMESITE=STRICT", attributes, StringComparison.Ordinal);
        Assert.Contains("MAX-AGE=43200", attributes, StringComparison.Ordinal); // 12h bound (CC-DSH-001 placeholder)
    }

    [Fact]
    [Requirement("CC-SEC-006")]
    public async Task Revoked_session_is_treated_as_unauthenticated_on_its_next_request()
    {
        var (client, sessionId) = await SignInAsync();
        using var signedIn = client;
        var before = await signedIn.GetAsync(Url("/__test/session/whoami"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal("alice", await before.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        // Server-side revocation from a separate, cookie-free client: the
        // signed-in browser never deletes its cookie (issue 061 AC-02).
        using var revoker = _factory.CreateHttpsClient();
        var revoke = await revoker.PostAsync(
            Url($"/__test/session/revoke/{sessionId}"), null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);

        var after = await signedIn.GetAsync(Url("/__test/session/whoami"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    [Requirement("CC-SEC-006")]
    public async Task Session_without_session_id_claim_is_rejected_fail_closed()
    {
        var (client, _) = await SignInAsync(omitSessionId: true);
        using var signedIn = client;

        var response = await signedIn.GetAsync(Url("/__test/session/whoami"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Requirement("CC-SEC-006")]
    public async Task Revocation_store_failure_denies_the_session_instead_of_bypassing()
    {
        using var factory = TestHostBuilder.Create(configureServices: services =>
            services.Replace(ServiceDescriptor.Singleton<ISessionRevocation>(new ThrowingSessionRevocation())));
        var (client, _) = await SignInAsync(factory);
        using var signedIn = client;

        var response = await signedIn.GetAsync(Url("/__test/session/whoami"), TestContext.Current.CancellationToken);

        // Fail closed (SECURITY.md, Logging rule 2): a broken revocation
        // check is a denial, never an authenticated session.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Requirement("CC-SEC-006")]
    public async Task Each_sign_in_issues_a_fresh_session_identifier()
    {
        var (first, firstId) = await SignInAsync();
        using var a = first;
        var (second, secondId) = await SignInAsync();
        using var b = second;

        Assert.NotEqual(firstId, secondId);
        Assert.True(firstId.Length >= 43, "session id must carry >= 256 bits of entropy");
    }

    [Fact]
    [Requirement("CC-SEC-006")]
    public async Task Cookie_authenticated_state_change_without_antiforgery_token_is_rejected_and_does_not_execute()
    {
        var (client, _) = await SignInAsync();
        using var signedIn = client;

        var rejected = await signedIn.PostAsync(
            Url("/__test/antiforgery/mutate"), null, TestContext.Current.CancellationToken);
        var count = await signedIn.GetAsync(Url("/__test/antiforgery/count"), TestContext.Current.CancellationToken);

        // Issue 061 AC-04/AC-05: 400 with an RFC 9457 body, and the state
        // change MUST NOT occur.
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        Assert.Equal("0", await count.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [Requirement("CC-SEC-006")]
    public async Task Cookie_authenticated_state_change_with_valid_antiforgery_token_succeeds()
    {
        var (client, _) = await SignInAsync();
        using var signedIn = client;

        var tokenResponse = await signedIn.GetAsync(Url("/__test/antiforgery/token"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        var token = await tokenResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, Url("/__test/antiforgery/mutate"));
        request.Headers.Add(CacheCowAntiforgery.HeaderName, token);
        var accepted = await signedIn.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("1", await accepted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [Requirement("CC-SEC-006")]
    public async Task Antiforgery_cookie_carries_the_same_hardened_attributes()
    {
        var (client, _) = await SignInAsync();
        using var signedIn = client;

        var tokenResponse = await signedIn.GetAsync(Url("/__test/antiforgery/token"), TestContext.Current.CancellationToken);

        var antiforgeryCookie = Assert.Single(
            tokenResponse.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(".AspNetCore.Antiforgery", StringComparison.OrdinalIgnoreCase));
        var attributes = antiforgeryCookie.ToUpperInvariant();
        Assert.Contains("HTTPONLY", attributes, StringComparison.Ordinal);
        Assert.Contains("SECURE", attributes, StringComparison.Ordinal);
        Assert.Contains("SAMESITE=STRICT", attributes, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-SEC-006")]
    public async Task Bearer_token_request_without_cookies_is_exempt_from_antiforgery()
    {
        using var client = _factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "b2b-client");
        client.DefaultRequestHeaders.Add("Authorization", "Bearer test-opaque-token");

        var response = await client.PostAsync(
            Url("/__test/antiforgery/mutate"), null, TestContext.Current.CancellationToken);

        // Issue 061 AC-06: no ambient cookie credential -> no CSRF surface.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Requirement("CC-SEC-006")]
    public async Task Safe_methods_with_a_session_cookie_are_exempt_from_antiforgery()
    {
        var (client, _) = await SignInAsync();
        using var signedIn = client;

        var response = await signedIn.GetAsync(Url("/__test/session/whoami"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // AC-08: authenticated responses are never cacheable (SECURITY.md,
        // HTTP boundary rules 3 and 10).
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }
}

/// <summary>Fault-injected revocation store proving fail-closed session validation.</summary>
internal sealed class ThrowingSessionRevocation : ISessionRevocation
{
    public ValueTask RevokeAsync(string sessionId, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("revocation store unavailable (test fault injection)");

    public ValueTask<bool> IsRevokedAsync(string sessionId, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("revocation store unavailable (test fault injection)");
}
