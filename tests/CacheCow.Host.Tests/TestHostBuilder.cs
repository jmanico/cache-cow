using System.Security.Claims;
using System.Text.Encodings.Web;
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
        string? staticRoot = null)
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
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, null);

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

/// <summary>Authenticates when the X-Test-User header is present; otherwise no result.</summary>
internal sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public new const string Scheme = "Test";
    public const string UserHeader = "X-Test-User";

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

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, user.ToString())], Scheme);
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
