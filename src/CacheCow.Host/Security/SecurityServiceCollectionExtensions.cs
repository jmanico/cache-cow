using System.Diagnostics;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CacheCow.Host.Security;

/// <summary>
/// Registers the host security services for issues 016-022: validated
/// security options (fail-closed at startup), HSTS, CORS, rate limiting,
/// RFC 9457 problem details with correlation IDs, deny-by-default
/// authorization with a fail-closed authorization service, and the structured
/// security-event logger.
/// </summary>
public static class SecurityServiceCollectionExtensions
{
    public static IServiceCollection AddCacheCowSecurity(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Options: bound from configuration, validated fail-closed at startup
        // (SECURITY.md, Logging rule 2; issues 016-019 "On System Error").
        services.AddOptions<SecurityOptions>()
            .BindConfiguration(SecurityOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<SecurityOptions>, SecurityOptionsValidator>();

        // Issue 022: structured security-event logging.
        services.AddSingleton<ISecurityEventLogger, SecurityEventLogger>();

        // Issue 016: HSTS with preload.
        services.AddSingleton<IConfigureOptions<HstsOptions>, HstsOptionsConfigurator>();

        // Issue 018: CORS exact-origin allowlist.
        services.AddCors();
        services.AddSingleton<IConfigureOptions<CorsOptions>, CorsOptionsConfigurator>();

        // Issue 019: baseline rate limiting with named stricter policy classes.
        services.AddRateLimiter(_ => { });
        services.AddSingleton<IConfigureOptions<RateLimiterOptions>, RateLimiterOptionsConfigurator>();

        // Issue 021: RFC 9457 problem details everywhere, with a correlation
        // ID (W3C trace context) and never internal details (SECURITY.md,
        // Logging rule 1; CC-API-006).
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["correlationId"] =
                Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);

        // Issue 020 + 016 AC-05: authentication registered so authorization
        // always evaluates a principal. Placeholder scheme until the Entra
        // providers land (issues 054/058/060); it never authenticates and
        // challenges with 401.
        services.AddAuthentication(CacheCowAuthentication.PlaceholderScheme)
            .AddScheme<AuthenticationSchemeOptions, UnconfiguredAuthenticationHandler>(
                CacheCowAuthentication.PlaceholderScheme, null);

        // Issue 061 (CC-SEC-006): session cookie policy applied to every
        // cookie scheme, per-request revocation validation, session-id
        // lifecycle for sign-in/privilege-change refresh, host-wide cookie
        // policy backstop, and global antiforgery enforcement.
        services.AddSingleton<ISessionRevocation, InMemorySessionRevocation>();
        services.AddSingleton<ISessionLifecycle, SessionLifecycle>();
        services.AddTransient<RevocationValidatingCookieEvents>();
        services.AddSingleton<IConfigureOptions<CookieAuthenticationOptions>, SessionCookieAuthenticationConfigurator>();
        services.AddSingleton<IConfigureOptions<CookiePolicyOptions>, CookiePolicyOptionsConfigurator>();
        services.AddAntiforgery();
        services.AddSingleton<IConfigureOptions<AntiforgeryOptions>, AntiforgeryOptionsConfigurator>();

        // Issue 062 (CC-SEC-007): object-level authorization - every
        // caller-owned resource access proves owner-or-tenant match
        // server-side via the named owner policy.
        services.AddSingleton<IAuthorizationHandler, ResourceOwnershipHandler>();

        // Issue 020: deny by default - every endpoint requires an
        // authenticated user unless it explicitly opts out; access is granted
        // via named policies (SECURITY.md, Authentication rule 1).
        services.AddAuthorization(options =>
        {
            var requireAuthenticated = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
            options.FallbackPolicy = requireAuthenticated;
            options.DefaultPolicy = requireAuthenticated;

            // Issue 062 (CC-SEC-007; SECURITY.md, Authentication rule 9).
            options.AddPolicy(ResourceAuthorization.OwnerPolicy, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(ResourceOwnershipRequirement.Instance));
        });

        // Unmatched routes answer 404 rather than a 401 challenge
        // (SECURITY.md, Authentication rule 9 hardening default).
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, NotFoundForUnmatchedRoutesAuthorizationResultHandler>();

        // Issue 021: fail closed - an exception in authorization evaluation is
        // a denial, never a bypass (SECURITY.md, Logging rule 2).
        services.AddTransient<DefaultAuthorizationService>();
        services.Replace(ServiceDescriptor.Transient<IAuthorizationService>(provider =>
            new FailClosedAuthorizationService(
                provider.GetRequiredService<DefaultAuthorizationService>(),
                provider.GetRequiredService<ISecurityEventLogger>(),
                provider.GetRequiredService<ILogger<FailClosedAuthorizationService>>())));

        return services;
    }
}
