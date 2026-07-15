using Microsoft.Extensions.Options;

namespace CacheCow.Host.Security;

/// <summary>
/// Fail-closed startup validation of <see cref="SecurityOptions"/>: an invalid
/// security configuration prevents the host from starting rather than falling
/// back to a permissive default (SECURITY.md, Logging rule 2; issues 016-019
/// "On System Error"). In particular, any CSP payment-processor origin or CORS
/// origin that is a wildcard, suffix match, non-HTTPS, or not an exact origin
/// is rejected at configuration-load time (SECURITY.md, HTTP boundary rules 2
/// and 4; issue 017 AC-02, issue 018 AC-02).
/// </summary>
public sealed class SecurityOptionsValidator : IValidateOptions<SecurityOptions>
{
    public ValidateOptionsResult Validate(string? name, SecurityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (options.Hsts.MaxAgeDays < 1)
        {
            failures.Add("Security:Hsts:MaxAgeDays must be at least 1 day.");
        }

        ValidateOrigins(failures, options.Csp.PaymentProcessorOrigins.FormAction, "Security:Csp:PaymentProcessorOrigins:FormAction");
        ValidateOrigins(failures, options.Csp.PaymentProcessorOrigins.FrameSrc, "Security:Csp:PaymentProcessorOrigins:FrameSrc");
        ValidateOrigins(failures, options.Csp.PaymentProcessorOrigins.ConnectSrc, "Security:Csp:PaymentProcessorOrigins:ConnectSrc");
        ValidateOrigins(failures, options.Cors.AllowedOrigins, "Security:Cors:AllowedOrigins");

        if (options.Cors.AllowCredentials && options.Cors.AllowedOrigins.Count == 0)
        {
            failures.Add("Security:Cors:AllowCredentials requires an explicit non-empty exact-origin allowlist (SECURITY.md, HTTP boundary rule 4).");
        }

        if (options.RequestLimits.MaxRequestBodyBytes < 1)
        {
            failures.Add("Security:RequestLimits:MaxRequestBodyBytes must be positive.");
        }

        if (options.RequestLimits.MaxPageSize < 1)
        {
            failures.Add("Security:RequestLimits:MaxPageSize must be positive.");
        }

        if (options.RequestLimits.DefaultPageSize < 1 || options.RequestLimits.DefaultPageSize > options.RequestLimits.MaxPageSize)
        {
            failures.Add("Security:RequestLimits:DefaultPageSize must be between 1 and MaxPageSize.");
        }

        ValidateRatePolicy(failures, options.RateLimiting.Default, "Security:RateLimiting:Default");
        ValidateRatePolicy(failures, options.RateLimiting.Authentication, "Security:RateLimiting:Authentication");
        ValidateRatePolicy(failures, options.RateLimiting.OrderCreation, "Security:RateLimiting:OrderCreation");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateOrigins(List<string> failures, IEnumerable<string> origins, string settingPath)
    {
        foreach (var origin in origins)
        {
            if (!ExactOrigin.IsValid(origin))
            {
                failures.Add(
                    $"{settingPath} contains '{origin}', which is not a single exact HTTPS origin. " +
                    "Wildcards and suffix matches are prohibited (SECURITY.md, HTTP boundary rules 2 and 4).");
            }
        }
    }

    private static void ValidateRatePolicy(List<string> failures, RateLimitPolicySettings settings, string settingPath)
    {
        if (settings.PermitLimit < 1)
        {
            failures.Add($"{settingPath}:PermitLimit must be positive.");
        }

        if (settings.WindowSeconds < 1)
        {
            failures.Add($"{settingPath}:WindowSeconds must be positive.");
        }
    }
}
