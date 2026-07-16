using System.Security.Claims;
using System.Text.Encodings.Web;
using CacheCow.Host.Security;
using CacheCow.Host.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace CacheCow.Host.Tests;

/// <summary>
/// Builds the host under test: enables the test-only endpoint surface
/// (TestOnlyEndpoints - never enabled by shipped configuration), registers the
/// test authentication scheme and the throwing authorization policy, and
/// applies per-test configuration overrides.
/// </summary>
internal static class TestHostBuilder
{
    public static WebApplicationFactory<Program> Create(
        IDictionary<string, string?>? config = null,
        string? environment = null,
        string? staticRoot = null,
        Action<IServiceCollection>? configureServices = null)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            if (environment is not null)
            {
                builder.UseEnvironment(environment);
            }

            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    [TestOnlyEndpoints.ConfigurationFlag] = "true",
                    ["Security:Cors:AllowedOrigins:0"] = "https://storefront.test",
                    ["Security:RequestLimits:MaxRequestBodyBytes"] = "2048",
                };
                if (config is not null)
                {
                    foreach (var pair in config)
                    {
                        settings[pair.Key] = pair.Value;
                    }
                }

                configuration.AddInMemoryCollection(settings);
            });

            builder.ConfigureServices(services =>
            {
                services.AddSingleton<TestOnlyEndpoints.OrderCounter>();

                services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthHandler.Scheme;
                        options.DefaultChallengeScheme = TestAuthHandler.Scheme;
                        options.DefaultForbidScheme = TestAuthHandler.Scheme;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, null)
                    // Cookie scheme for the issue 061 probes; its options are
                    // configured by the product SessionCookieAuthenticationConfigurator,
                    // which is exactly what the cookie-attribute assertions verify.
                    .AddCookie(TestOnlyEndpoints.TestCookieScheme);

                services.AddAuthorization(options =>
                    options.AddPolicy("test-throwing", policy => policy.AddRequirements(new ThrowingRequirement())));
                services.AddSingleton<IAuthorizationHandler, ThrowingRequirementHandler>();

                // The framework excludes localhost from HSTS by default; the
                // suite asserts the header, so drop the exclusion in-test.
                services.Configure<HstsOptions>(options => options.ExcludedHosts.Clear());
                services.Configure<HttpsRedirectionOptions>(options => options.HttpsPort = 443);

                if (staticRoot is not null)
                {
                    services.Configure<StaticFileOptions>(options =>
                        options.FileProvider = new PhysicalFileProvider(staticRoot));
                }

                configureServices?.Invoke(services);
            });
        });
    }

    /// <summary>HTTPS client so HSTS applies and redirection does not interfere.</summary>
    public static HttpClient CreateHttpsClient(this WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });

    public static HttpClient CreateHttpClient(this WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"),
            AllowAutoRedirect = false,
        });
}

/// <summary>
/// Authenticates when the X-Test-User header is present; otherwise no result.
/// Optional X-Test-Tenant and X-Test-Roles (comma-separated) headers supply
/// the tenant claim and roles the object-level authorization suite scopes by
/// (issue 062).
///
/// Optional B2B claim headers let the composition suite exercise the real
/// /v1 pipeline (issue 054's claim policy in B2BTokenClaimsValidator):
/// X-Test-Client-Id maps to the "client_id" claim, X-Test-Scopes to "scp"
/// (space-separated). When a client id is present the handler also stamps
/// iat/exp for a 5-minute-old-to-valid window (within the CC-API-003 15-minute
/// ceiling) and — unless X-Test-Bearer-Only is set — an mTLS "cnf"
/// confirmation claim, so write scopes stay effective (bearer-only tokens are
/// ceilinged to read-only per CC-API-003).
/// </summary>
internal sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public new const string Scheme = TestOnlyEndpoints.TestHeaderScheme;
    public const string UserHeader = "X-Test-User";
    public const string TenantHeader = "X-Test-Tenant";
    public const string RolesHeader = "X-Test-Roles";
    public const string ClientIdHeader = "X-Test-Client-Id";
    public const string ScopesHeader = "X-Test-Scopes";
    public const string BearerOnlyHeader = "X-Test-Bearer-Only";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var user) || StringValues.IsNullOrEmpty(user))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.ToString()),
            new(ClaimTypes.NameIdentifier, user.ToString()),
        };

        if (Request.Headers.TryGetValue(TenantHeader, out var tenant) && !StringValues.IsNullOrEmpty(tenant))
        {
            claims.Add(new Claim(CacheCowClaims.TenantId, tenant.ToString()));
        }

        if (Request.Headers.TryGetValue(RolesHeader, out var roles) && !StringValues.IsNullOrEmpty(roles))
        {
            foreach (var role in roles.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        if (Request.Headers.TryGetValue(ClientIdHeader, out var clientId) && !StringValues.IsNullOrEmpty(clientId))
        {
            claims.Add(new Claim("client_id", clientId.ToString()));

            var now = DateTimeOffset.UtcNow;
            claims.Add(new Claim("iat", now.AddMinutes(-5).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)));
            claims.Add(new Claim("exp", now.AddMinutes(5).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)));

            if (!Request.Headers.ContainsKey(BearerOnlyHeader))
            {
                claims.Add(new Claim("cnf", /*lang=json,strict*/ """{"x5t#S256":"test-thumbprint"}"""));
            }
        }

        if (Request.Headers.TryGetValue(ScopesHeader, out var scopes) && !StringValues.IsNullOrEmpty(scopes))
        {
            claims.Add(new Claim("scp", scopes.ToString()));
        }

        var identity = new ClaimsIdentity(claims, Scheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>Requirement whose handler always throws - probes fail-closed authorization (issue 021 AC-03).</summary>
internal sealed class ThrowingRequirement : IAuthorizationRequirement
{
}

internal sealed class ThrowingRequirementHandler : AuthorizationHandler<ThrowingRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ThrowingRequirement requirement) =>
        throw new InvalidOperationException("authorization backend unavailable (test fault injection)");
}
