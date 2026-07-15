using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Options;

namespace CacheCow.Host.Security;

/// <summary>
/// HSTS with preload from configuration (SECURITY.md, HTTP boundary rule 1;
/// CC-SEC-003; issue 016 AC-01). The framework's default localhost exclusions
/// stay in place for development.
/// </summary>
internal sealed class HstsOptionsConfigurator : IConfigureOptions<HstsOptions>
{
    private readonly IOptions<SecurityOptions> _security;

    public HstsOptionsConfigurator(IOptions<SecurityOptions> security)
    {
        _security = security;
    }

    public void Configure(HstsOptions options)
    {
        var hsts = _security.Value.Hsts;
        options.MaxAge = TimeSpan.FromDays(hsts.MaxAgeDays);
        options.IncludeSubDomains = hsts.IncludeSubDomains;
        options.Preload = hsts.Preload;
    }
}

/// <summary>
/// CORS from configuration: explicit WithOrigins() allowlist of exact origins
/// only - the origins were already validated as exact HTTPS origins at
/// startup, so credentials can never combine with a wildcard or suffix match
/// (SECURITY.md, HTTP boundary rule 4; issue 018 AC-01/AC-02). An empty
/// allowlist (the shipped default) grants nothing: cross-origin requests
/// receive no Access-Control-Allow-Origin header.
/// </summary>
internal sealed class CorsOptionsConfigurator : IConfigureOptions<CorsOptions>
{
    public const string PolicyName = "cachecow-exact-origins";

    private readonly IOptions<SecurityOptions> _security;

    public CorsOptionsConfigurator(IOptions<SecurityOptions> security)
    {
        _security = security;
    }

    public void Configure(CorsOptions options)
    {
        var cors = _security.Value.Cors;
        options.AddPolicy(PolicyName, policy =>
        {
            if (cors.AllowedOrigins.Count > 0)
            {
                policy.WithOrigins([.. cors.AllowedOrigins])
                      .AllowAnyHeader()
                      .AllowAnyMethod();

                if (cors.AllowCredentials)
                {
                    policy.AllowCredentials();
                }
                else
                {
                    policy.DisallowCredentials();
                }
            }

            // No origins configured: the policy allows nothing (default deny).
        });
    }
}
