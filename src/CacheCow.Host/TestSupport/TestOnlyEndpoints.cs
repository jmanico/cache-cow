using System.Globalization;
using System.Security.Claims;
using CacheCow.Host.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace CacheCow.Host.TestSupport;

/// <summary>
/// TEST-ONLY sample endpoints. These are NOT product endpoints and carry no
/// CC-* feature scope (REQUIREMENTS.md §17: unreferenced product paths are
/// scope creep - these exist solely so the integration-test suite can observe
/// the security middleware of issues 016-022 and the session/CSRF and
/// object-level authorization controls of issues 061-062 in-process). They are mapped
/// only when the "CacheCow:TestSurface" configuration flag is true, which the
/// shipped configuration never sets; only the test project's
/// WebApplicationFactory enables it. Several endpoints resolve services (test
/// counter, test-only authorization policy) that exist only in test
/// composition, so the surface is non-functional outside the test host even
/// if the flag were set.
/// </summary>
public static class TestOnlyEndpoints
{
    public const string ConfigurationFlag = "CacheCow:TestSurface";

    /// <summary>Cookie scheme registered only by the test project (issue 061 probes).</summary>
    public const string TestCookieScheme = "TestCookie";

    /// <summary>Header scheme registered only by the test project.</summary>
    public const string TestHeaderScheme = "Test";

    /// <summary>
    /// Test-only caller-owned resource (issue 062 probes): OwnerId is a fake
    /// consumer identity or partner tenant; Secret is the marker the IDOR
    /// suite asserts never appears in an out-of-scope response.
    /// </summary>
    public sealed record SampleResource(string Id, string OwnerId, string Secret) : ITenantOwnedResource;

    private static readonly Dictionary<string, SampleResource> SampleResources = new(StringComparer.Ordinal)
    {
        ["orders/ord-a-1"] = new("ord-a-1", "tenant-a", "order-secret-tenant-a"),
        ["orders/ord-b-1"] = new("ord-b-1", "tenant-b", "order-secret-tenant-b"),
        ["invoices/inv-a-1"] = new("inv-a-1", "tenant-a", "invoice-secret-tenant-a"),
        ["addresses/addr-alice-1"] = new("addr-alice-1", "alice", "address-secret-alice"),
        ["partners/partner-a"] = new("partner-a", "tenant-a", "partner-terms-tenant-a"),
        ["ops-notes/note-a-1"] = new("note-a-1", "tenant-a", "ops-note-secret-tenant-a"),
    };

    private static SampleResource? FindSample(string category, string id) =>
        SampleResources.GetValueOrDefault(category + "/" + id);

    /// <summary>Test-only order counter proving no state change occurs on rejected requests.</summary>
    public sealed class OrderCounter
    {
        private int _count;

        public int Value => Volatile.Read(ref _count);

        public int Increment() => Interlocked.Increment(ref _count);
    }

    public sealed record EchoPayload(string? Message);

    public static void MapIfEnabled(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!app.Configuration.GetValue<bool>(ConfigurationFlag))
        {
            return;
        }

        var group = app.MapGroup("/__test");

        // Deny-by-default probes (issue 020): no authorization metadata at all
        // -> fallback policy applies.
        group.MapGet("/protected", () => Results.Text("protected-ok"));
        group.MapGet("/default-authz", () => Results.Text("default-authz-ok")).RequireAuthorization();
        group.MapGet("/anonymous", () => Results.Text("anonymous-ok")).AllowAnonymous();

        // Fail-closed authorization probe (issue 021 AC-03): the
        // "test-throwing" policy and its throwing handler are registered by
        // the test project only.
        group.MapGet("/throwing-authz", () => Results.Text("never-served")).RequireAuthorization("test-throwing");

        // Header probes (issues 016/017).
        group.MapGet("/html", (HttpContext context) => Results.Content(
                $"<!doctype html><html><head><title>test</title></head><body>" +
                $"<script nonce=\"{CspNonce.Get(context)}\">/* nonce probe */</script></body></html>",
                "text/html"))
            .AllowAnonymous();
        group.MapGet("/sensitive", () => Results.Text("sensitive-ok"))
            .AllowAnonymous()
            .WithMetadata(new SensitiveResponseAttribute());

        // Method/media/size probes (issue 018).
        group.MapGet("/get-only", () => Results.Text("get-ok")).AllowAnonymous();
        group.MapPost("/echo-json", (EchoPayload payload) => Results.Json(payload)).AllowAnonymous();
        group.MapGet("/page", (int? pageSize, IOptions<SecurityOptions> options) =>
                Results.Text(PageSizeLimiter.Clamp(pageSize, options.Value.RequestLimits)
                    .ToString(CultureInfo.InvariantCulture)))
            .AllowAnonymous();

        // Rate-limit probes (issue 019).
        group.MapGet("/limited-auth", () => Results.Text("limited-auth-ok"))
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Authentication);
        group.MapPost("/limited-order", (OrderCounter counter) =>
                Results.Text(counter.Increment().ToString(CultureInfo.InvariantCulture)))
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.OrderCreation);

        // Error-shape probe (issue 021): the exception message simulates
        // internal detail that must never reach a client.
        group.MapGet("/throws", string () =>
                throw new InvalidOperationException(
                    "internal-diagnostic-detail: Server=sql.internal;Database=cachecow"))
            .AllowAnonymous();

        MapSessionAndAntiforgeryProbes(group);
        MapSampleResourceProbes(group);
    }

    /// <summary>
    /// Issue 061 probes (CC-SEC-006): cookie session sign-in/whoami/revoke and
    /// antiforgery. The cookie scheme is registered only in test composition;
    /// the session id claim is stamped through the product ISessionLifecycle.
    /// </summary>
    private static void MapSessionAndAntiforgeryProbes(RouteGroupBuilder group)
    {
        // Sign-in is anonymous (no ambient cookie exists yet, so no CSRF
        // surface); returns the session id so tests can revoke it
        // server-side. omitSessionId simulates a broken sign-in flow that
        // forgets the session claim - the next request must be rejected.
        group.MapPost("/session/sign-in", async (HttpContext http, string user, bool? omitSessionId, ISessionLifecycle lifecycle) =>
            {
                var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, user)], TestCookieScheme);
                var sessionId = omitSessionId == true ? "(none)" : lifecycle.BeginSession(identity);
                await http.SignInAsync(TestCookieScheme, new ClaimsPrincipal(identity));
                return Results.Text(sessionId);
            })
            .AllowAnonymous();

        group.MapGet("/session/whoami", (ClaimsPrincipal user) => Results.Text(user.Identity?.Name ?? string.Empty))
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(TestCookieScheme)
                .RequireAuthenticatedUser());

        // Server-side revocation trigger (admin action / security event
        // stand-in). Anonymous and cookie-free in tests, so antiforgery does
        // not apply; the revoked session's next request must be rejected.
        group.MapPost("/session/revoke/{sessionId}", async (string sessionId, ISessionRevocation revocation) =>
            {
                await revocation.RevokeAsync(sessionId);
                return Results.Text("revoked");
            })
            .AllowAnonymous();

        // Antiforgery token issuance for the cookie-authenticated session
        // (the CSP-compatible SPA pattern: fetch token, send it back in the
        // X-CSRF-TOKEN header on state-changing requests).
        group.MapGet("/antiforgery/token", (HttpContext http, IAntiforgery antiforgery) =>
                Results.Text(antiforgery.GetAndStoreTokens(http).RequestToken ?? string.Empty))
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(TestCookieScheme)
                .RequireAuthenticatedUser());

        // State-changing probe reachable by both the cookie session and the
        // header scheme (the latter stands in for the bearer-token surface).
        // The counter proves no state change occurs on rejected requests.
        group.MapPost("/antiforgery/mutate", (OrderCounter counter) =>
                Results.Text(counter.Increment().ToString(CultureInfo.InvariantCulture)))
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(TestCookieScheme, TestHeaderScheme)
                .RequireAuthenticatedUser());

        group.MapGet("/antiforgery/count", (OrderCounter counter) =>
                Results.Text(counter.Value.ToString(CultureInfo.InvariantCulture)))
            .AllowAnonymous();
    }

    /// <summary>
    /// Issue 062 probes (CC-SEC-007, CC-QA-005): sample caller-owned resources
    /// (orders, invoices, addresses, partner data - the mandatory IDOR
    /// classes) owned by fake tenants, all going through the product
    /// ResourceAuthorization helper so the IDOR matrix exercises the real
    /// enforcement path. Real module endpoints plug their routes into the
    /// same test harness as they land.
    /// </summary>
    private static void MapSampleResourceProbes(RouteGroupBuilder group)
    {
        var resources = group.MapGroup("/resources");

        resources.MapGet("/orders/{id}", (string id, ClaimsPrincipal user, IAuthorizationService authorization) =>
            ResourceAuthorization.RequireOwnedResourceAsync(
                authorization, user, FindSample("orders", id), r => Results.Text(r.Secret)));

        resources.MapGet("/invoices/{id}", (string id, ClaimsPrincipal user, IAuthorizationService authorization) =>
            ResourceAuthorization.RequireOwnedResourceAsync(
                authorization, user, FindSample("invoices", id), r => Results.Text(r.Secret)));

        resources.MapGet("/addresses/{id}", (string id, ClaimsPrincipal user, IAuthorizationService authorization) =>
            ResourceAuthorization.RequireOwnedResourceAsync(
                authorization, user, FindSample("addresses", id), r => Results.Text(r.Secret)));

        // Mutation probe: cross-tenant writes must be refused identically.
        resources.MapPost("/partners/{id}/orders", (string id, ClaimsPrincipal user, IAuthorizationService authorization) =>
            ResourceAuthorization.RequireOwnedResourceAsync(
                authorization, user, FindSample("partners", id), _ => Results.Text("partner-order-accepted")));

        // Role-gated resource endpoint: an authenticated caller with the
        // wrong role gets 404, not 403 (ResourceEndpointAttribute mapping).
        resources.MapGet("/ops-notes/{id}", (string id, ClaimsPrincipal user, IAuthorizationService authorization) =>
                ResourceAuthorization.RequireOwnedResourceAsync(
                    authorization, user, FindSample("ops-notes", id), r => Results.Text(r.Secret)))
            .RequireAuthorization(policy => policy.RequireRole("ops-agent"))
            .WithMetadata(new ResourceEndpointAttribute());

        // Fail-closed probe: the "test-throwing" policy's handler (registered
        // by the test project) throws during resource authorization - the
        // outcome must be the same 404 denial, never the resource.
        resources.MapGet("/faulted/{id}", (string id, ClaimsPrincipal user, IAuthorizationService authorization) =>
            ResourceAuthorization.RequireOwnedResourceAsync(
                authorization, user, FindSample("orders", id), r => Results.Text(r.Secret), "test-throwing"));
    }
}
