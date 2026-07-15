using System.Net;
using System.Security.Claims;
using System.Text;
using CacheCow.Modules.WholesaleB2B.Auth;
using CacheCow.Modules.WholesaleB2B.Partners;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.WholesaleB2B.Tests;

/// <summary>
/// Issue 054 (CC-API-002/003): the claim-level token policy the module owns —
/// client-id → partner mapping, ≤ 15-minute token lifetime, sender-constraint
/// recording with the bearer-only read-only ceiling, closed scope model, and
/// fail-closed behavior for every unverifiable input. JWT signature validation
/// itself is host JwtBearer scope (SECURITY.md, Authentication rule 7).
/// </summary>
public sealed class B2BTokenPolicyTests
{
    private static readonly DateTimeOffset Now = Fixtures.T0;

    private static ClaimsPrincipal Principal(
        string? clientId = "client-a",
        string? scopes = "orders:read",
        string? cnf = """{"x5t#S256":"thumb"}""",
        long? iat = null,
        long? exp = null,
        bool authenticated = true)
    {
        var claims = new List<Claim>();
        if (clientId is not null)
        {
            claims.Add(new Claim("client_id", clientId));
        }

        if (scopes is not null)
        {
            claims.Add(new Claim("scp", scopes));
        }

        if (cnf is not null)
        {
            claims.Add(new Claim("cnf", cnf));
        }

        claims.Add(new Claim("iat", (iat ?? Now.AddSeconds(-60).ToUnixTimeSeconds()).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        claims.Add(new Claim("exp", (exp ?? Now.AddMinutes(10).ToUnixTimeSeconds()).ToString(System.Globalization.CultureInfo.InvariantCulture)));

        var identity = authenticated ? new ClaimsIdentity(claims, "test") : new ClaimsIdentity(claims);
        return new ClaimsPrincipal(identity);
    }

    private static B2BTokenClaimsValidator Validator(out FakeB2BClientDirectory directory)
    {
        var workflow = new PartnerOnboardingWorkflow(new RecordingPartnerAuditSink());
        directory = new FakeB2BClientDirectory();
        directory.Add("client-a", Fixtures.TenantIn(PartnerOnboardingState.Approved, workflow, "partner-a", Fixtures.DeIdentity()));
        directory.Add("client-suspended", Fixtures.TenantIn(PartnerOnboardingState.Suspended, workflow, "partner-x", Fixtures.DeIdentity()));
        return new B2BTokenClaimsValidator(directory, new ManualTimeProvider(Now));
    }

    [Fact]
    [Requirement("CC-API-002")]
    public void A_valid_sender_constrained_token_mints_a_tenant_scoped_context()
    {
        var result = Validator(out _).Validate(Principal(scopes: "catalog:read orders:write"));

        Assert.True(result.Succeeded);
        var context = result.Context!;
        Assert.Equal("client-a", context.ClientId);
        Assert.Equal(PartnerId.Parse("partner-a"), context.Tenant.PartnerId);
        Assert.Equal(B2BSenderConstraint.MutualTls, context.SenderConstraint);
        Assert.Contains("orders:write", context.EffectiveScopes);
    }

    [Fact]
    [Requirement("CC-API-003")]
    public void A_token_minted_with_a_lifetime_over_15_minutes_is_rejected()
    {
        var result = Validator(out _).Validate(Principal(
            iat: Now.ToUnixTimeSeconds(),
            exp: Now.AddMinutes(16).ToUnixTimeSeconds()));

        Assert.False(result.Succeeded);
        Assert.Equal("lifetime-exceeds-maximum", result.FailureReason);
    }

    [Fact]
    [Requirement("CC-API-003")]
    public void A_token_that_cannot_prove_its_lifetime_is_rejected()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("client_id", "client-a"), new Claim("exp", Now.AddMinutes(5).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture))],
            "test"));

        var result = Validator(out _).Validate(principal);

        Assert.False(result.Succeeded);
        Assert.Equal("unverifiable-lifetime", result.FailureReason);
    }

    [Fact]
    [Requirement("CC-API-003")]
    public void An_expired_token_is_rejected_beyond_the_two_minute_skew()
    {
        var validator = Validator(out _);

        // Expired 3 minutes ago: outside the ≤ 2-minute ratified skew.
        var expired = validator.Validate(Principal(
            iat: Now.AddMinutes(-13).ToUnixTimeSeconds(),
            exp: Now.AddMinutes(-3).ToUnixTimeSeconds()));
        Assert.False(expired.Succeeded);
        Assert.Equal("outside-validity-window", expired.FailureReason);

        // Expired 1 minute ago: inside the skew allowance (host JwtBearer is
        // the primary lifetime gate; this module only re-checks).
        var withinSkew = validator.Validate(Principal(
            iat: Now.AddMinutes(-11).ToUnixTimeSeconds(),
            exp: Now.AddMinutes(-1).ToUnixTimeSeconds()));
        Assert.True(withinSkew.Succeeded);
    }

    [Fact]
    [Requirement("CC-API-003")]
    public void A_bearer_only_token_is_ceilinged_to_read_only_scopes()
    {
        var result = Validator(out _).Validate(Principal(scopes: "catalog:read orders:write", cnf: null));

        Assert.True(result.Succeeded);
        var context = result.Context!;
        Assert.Equal(B2BSenderConstraint.None, context.SenderConstraint);
        Assert.Contains("orders:write", context.GrantedScopes);
        Assert.DoesNotContain("orders:write", context.EffectiveScopes);
        Assert.Contains("catalog:read", context.EffectiveScopes);
    }

    [Fact]
    [Requirement("CC-API-003")]
    public void A_malformed_cnf_claim_degrades_to_bearer_only_never_to_constrained()
    {
        var result = Validator(out _).Validate(Principal(scopes: "orders:write", cnf: "not-json"));

        Assert.True(result.Succeeded);
        Assert.Equal(B2BSenderConstraint.None, result.Context!.SenderConstraint);
        Assert.Empty(result.Context.EffectiveScopes);
    }

    [Fact]
    [Requirement("CC-API-003")]
    public void A_dpop_bound_token_is_recorded_as_sender_constrained()
    {
        var result = Validator(out _).Validate(Principal(scopes: "orders:write", cnf: """{"jkt":"key-thumb"}"""));

        Assert.True(result.Succeeded);
        Assert.Equal(B2BSenderConstraint.DPoP, result.Context!.SenderConstraint);
        Assert.Contains("orders:write", result.Context.EffectiveScopes);
    }

    [Fact]
    [Requirement("CC-API-004")]
    public void A_token_carrying_an_unknown_scope_is_rejected_outright()
    {
        var result = Validator(out _).Validate(Principal(scopes: "orders:read admin:everything"));

        Assert.False(result.Succeeded);
        Assert.Equal("unknown-scope", result.FailureReason);
    }

    [Fact]
    [Requirement("CC-API-002")]
    public void An_unregistered_client_id_is_rejected()
    {
        var result = Validator(out _).Validate(Principal(clientId: "client-unknown"));

        Assert.False(result.Succeeded);
        Assert.Equal("unregistered-client", result.FailureReason);
    }

    [Fact]
    [Requirement("CC-API-002")]
    public void A_suspended_partners_client_is_rejected()
    {
        var result = Validator(out _).Validate(Principal(clientId: "client-suspended"));

        Assert.False(result.Succeeded);
        Assert.Equal("partner-not-approved", result.FailureReason);
    }

    [Fact]
    [Requirement("CC-API-002")]
    public void A_directory_fault_is_a_denial_never_a_bypass()
    {
        var validator = Validator(out var directory);
        directory.Throw = true;

        var result = validator.Validate(Principal());

        Assert.False(result.Succeeded);
        Assert.Equal("client-directory-unavailable", result.FailureReason);
    }

    [Fact]
    [Requirement("CC-API-002")]
    public void An_unauthenticated_principal_or_missing_client_id_is_rejected()
    {
        var validator = Validator(out _);

        Assert.False(validator.Validate(Principal(authenticated: false)).Succeeded);
        Assert.False(validator.Validate(Principal(clientId: null)).Succeeded);
    }

    // ---- endpoint-level enforcement -----------------------------------------

    [Fact]
    [Requirement("CC-API-002")]
    public async Task An_unauthenticated_request_receives_401()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/v1/catalog/DE", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Requirement("CC-API-002")]
    public async Task A_credential_in_the_query_string_is_rejected_with_401()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();

        using var request = B2BApiTestHost.Request(
            HttpMethod.Get, "/v1/catalog/DE?access_token=secret", B2BApiTestHost.ClientA, "catalog:read");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Requirement("CC-API-003")]
    public async Task An_over_lifetime_token_is_rejected_at_every_endpoint()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();

        var now = DateTimeOffset.UtcNow;
        using var request = B2BApiTestHost.Request(
            HttpMethod.Get, "/v1/catalog/DE", B2BApiTestHost.ClientA, "catalog:read",
            issuedAtUnix: now.ToUnixTimeSeconds(),
            expiresAtUnix: now.AddMinutes(30).ToUnixTimeSeconds());
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Requirement("CC-API-003")]
    public async Task A_bearer_only_token_with_orders_write_is_denied_on_the_mutating_endpoint()
    {
        await using var host = await B2BApiTestHost.StartAsync();
        using var client = host.CreateClient();

        var request = B2BApiTestHost.Request(
            HttpMethod.Post, "/v1/orders", B2BApiTestHost.ClientA, "orders:write orders:read",
            senderConstrained: false);
        request.Headers.Add("Idempotency-Key", "idem-bearer");
        request.Content = new StringContent(
            """{"market":"DE","lines":[{"sku":"SKU-BRISKET","cases":1}]}""", Encoding.UTF8, "application/json");
        using var owned = request;
        using var response = await client.SendAsync(owned, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // The same bearer token still reads (read-only ceiling, not a lockout).
        using var read = B2BApiTestHost.Request(
            HttpMethod.Get, "/v1/catalog/DE", B2BApiTestHost.ClientA, "catalog:read", senderConstrained: false);
        using var readResponse = await client.SendAsync(read, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
    }
}
