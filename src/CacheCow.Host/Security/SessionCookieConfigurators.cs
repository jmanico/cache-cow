using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.Extensions.Options;

namespace CacheCow.Host.Security;

/// <summary>
/// Applies the session cookie policy of CC-SEC-006 (SECURITY.md,
/// Authentication rule 11) to EVERY cookie authentication scheme registered in
/// the host, present and future (issues 058/060 schemes inherit it
/// automatically): HttpOnly, Secure (TLS-only per HTTP boundary rule 1),
/// SameSite from configuration, bounded absolute expiry (no sliding renewal -
/// the ticket and the cookie both expire at the configured bound), and the
/// per-request revocation check via <see cref="RevocationValidatingCookieEvents"/>.
/// </summary>
internal sealed class SessionCookieAuthenticationConfigurator : IConfigureNamedOptions<CookieAuthenticationOptions>
{
    private readonly IOptions<SecurityOptions> _security;

    public SessionCookieAuthenticationConfigurator(IOptions<SecurityOptions> security)
    {
        _security = security;
    }

    public void Configure(string? name, CookieAuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var settings = _security.Value.SessionCookies;
        var lifetime = TimeSpan.FromHours(settings.MaxAgeHours);

        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = settings.ParsedSameSite;
        options.Cookie.MaxAge = lifetime;
        options.ExpireTimeSpan = lifetime;
        options.SlidingExpiration = false;
        options.EventsType = typeof(RevocationValidatingCookieEvents);
    }

    public void Configure(CookieAuthenticationOptions options) => Configure(Options.DefaultName, options);
}

/// <summary>
/// Host-wide cookie policy backstop (CC-SEC-006): no response cookie leaves
/// the host without HttpOnly, Secure, and at least the configured SameSite
/// mode - covering cookies appended by anything other than the configured
/// authentication schemes (antiforgery, future framework cookies). Enforced by
/// the CookiePolicy stage of <see cref="SecurityPipeline"/>.
/// </summary>
internal sealed class CookiePolicyOptionsConfigurator : IConfigureOptions<CookiePolicyOptions>
{
    private readonly IOptions<SecurityOptions> _security;

    public CookiePolicyOptionsConfigurator(IOptions<SecurityOptions> security)
    {
        _security = security;
    }

    public void Configure(CookiePolicyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.HttpOnly = HttpOnlyPolicy.Always;
        options.Secure = CookieSecurePolicy.Always;
        options.MinimumSameSitePolicy = _security.Value.SessionCookies.ParsedSameSite;
    }
}

/// <summary>
/// Antiforgery token configuration (CC-SEC-006; SECURITY.md, Authentication
/// rule 11): the request token travels in the <see cref="CacheCowAntiforgery.HeaderName"/>
/// header - a CSP-compatible pattern for the Angular clients (no inline
/// script; the SPA fetches the token from a same-origin endpoint and attaches
/// the header on state-changing XHR, which a cross-site page cannot do) - and
/// the antiforgery cookie carries the same HttpOnly/Secure/SameSite posture as
/// session cookies.
/// </summary>
internal sealed class AntiforgeryOptionsConfigurator : IConfigureOptions<AntiforgeryOptions>
{
    private readonly IOptions<SecurityOptions> _security;

    public AntiforgeryOptionsConfigurator(IOptions<SecurityOptions> security)
    {
        _security = security;
    }

    public void Configure(AntiforgeryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.HeaderName = CacheCowAntiforgery.HeaderName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = _security.Value.SessionCookies.ParsedSameSite;
    }
}
