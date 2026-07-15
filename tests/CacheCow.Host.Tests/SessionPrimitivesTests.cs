using System.Security.Claims;
using CacheCow.Host.Security;
using CacheCow.Host.TestSupport;
using CacheCow.SharedKernel.Testing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CacheCow.Host.Tests;

/// <summary>
/// Issue 061 unit coverage (CC-SEC-006; SECURITY.md, Authentication rule 11):
/// session-id generation, the in-memory revocation store, session refresh on
/// sign-in and privilege change, and the per-request cookie validation hook
/// including its fail-closed behavior.
/// </summary>
public sealed class SessionPrimitivesTests
{
    private static SecurityEventLogger NullEvents { get; } = new(NullLogger<SecurityEventLogger>.Instance);

    [Fact]
    [Requirement("CC-SEC-006")]
    public void Session_ids_are_unique_and_high_entropy()
    {
        var ids = Enumerable.Range(0, 1000).Select(_ => SessionTokens.NewSessionId()).ToArray();

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ids, id => Assert.True(id.Length >= 43, "256 bits base64url-encoded is 43 characters"));
    }

    [Fact]
    [Requirement("CC-SEC-006")]
    public async Task Revocation_store_revokes_idempotently_and_reports_status()
    {
        var store = new InMemorySessionRevocation();

        Assert.False(await store.IsRevokedAsync("session-1", TestContext.Current.CancellationToken));
        await store.RevokeAsync("session-1", TestContext.Current.CancellationToken);
        await store.RevokeAsync("session-1", TestContext.Current.CancellationToken);
        Assert.True(await store.IsRevokedAsync("session-1", TestContext.Current.CancellationToken));
        Assert.False(await store.IsRevokedAsync("session-2", TestContext.Current.CancellationToken));
    }

    [Fact]
    [Requirement("CC-SEC-006")]
    public void BeginSession_stamps_exactly_one_fresh_session_id_claim()
    {
        var lifecycle = new SessionLifecycle(new InMemorySessionRevocation());
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice")], "test");

        var first = lifecycle.BeginSession(identity);
        var second = lifecycle.BeginSession(identity);

        // Sign-in never carries a prior session identifier over (fixation).
        Assert.NotEqual(first, second);
        var claim = Assert.Single(identity.FindAll(CacheCowClaims.SessionId));
        Assert.Equal(second, claim.Value);
    }

    [Fact]
    [Requirement("CC-SEC-006")]
    public async Task RefreshSession_issues_a_new_id_and_revokes_the_previous_one()
    {
        var store = new InMemorySessionRevocation();
        var lifecycle = new SessionLifecycle(store);
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice")], "test");
        var original = lifecycle.BeginSession(identity);

        // Privilege change (issue 061 AC-03; SECURITY.md, Authentication rule 11).
        var refreshed = await lifecycle.RefreshSessionAsync(identity, TestContext.Current.CancellationToken);

        Assert.NotEqual(original, refreshed);
        Assert.True(await store.IsRevokedAsync(original, TestContext.Current.CancellationToken));
        Assert.False(await store.IsRevokedAsync(refreshed, TestContext.Current.CancellationToken));
        var claim = Assert.Single(identity.FindAll(CacheCowClaims.SessionId));
        Assert.Equal(refreshed, claim.Value);
    }

    [Fact]
    [Requirement("CC-SEC-006")]
    public async Task Cookie_principal_with_live_session_id_is_kept()
    {
        var events = new RevocationValidatingCookieEvents(new InMemorySessionRevocation(), NullEvents);
        var context = CreateValidateContext(SessionTokens.NewSessionId());

        await events.ValidatePrincipal(context);

        Assert.NotNull(context.Principal);
    }

    [Fact]
    [Requirement("CC-SEC-006")]
    public async Task Cookie_principal_with_revoked_session_id_is_rejected()
    {
        var store = new InMemorySessionRevocation();
        await store.RevokeAsync("revoked-session", TestContext.Current.CancellationToken);
        var events = new RevocationValidatingCookieEvents(store, NullEvents);
        var context = CreateValidateContext("revoked-session");

        await events.ValidatePrincipal(context);

        Assert.Null(context.Principal);
    }

    [Fact]
    [Requirement("CC-SEC-006")]
    public async Task Cookie_principal_without_session_id_claim_is_rejected()
    {
        var events = new RevocationValidatingCookieEvents(new InMemorySessionRevocation(), NullEvents);
        var context = CreateValidateContext(sessionId: null);

        await events.ValidatePrincipal(context);

        Assert.Null(context.Principal);
    }

    [Fact]
    [Requirement("CC-SEC-006")]
    public async Task Revocation_check_exception_rejects_the_principal_fail_closed()
    {
        var events = new RevocationValidatingCookieEvents(new ThrowingSessionRevocation(), NullEvents);
        var context = CreateValidateContext(SessionTokens.NewSessionId());

        await events.ValidatePrincipal(context);

        Assert.Null(context.Principal);
    }

    private static CookieValidatePrincipalContext CreateValidateContext(string? sessionId)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, "alice") };
        if (sessionId is not null)
        {
            claims.Add(new Claim(CacheCowClaims.SessionId, sessionId));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, TestOnlyEndpoints.TestCookieScheme));
        return new CookieValidatePrincipalContext(
            new DefaultHttpContext(),
            new AuthenticationScheme(
                TestOnlyEndpoints.TestCookieScheme,
                displayName: null,
                typeof(CookieAuthenticationHandler)),
            new CookieAuthenticationOptions(),
            new AuthenticationTicket(principal, TestOnlyEndpoints.TestCookieScheme));
    }
}
