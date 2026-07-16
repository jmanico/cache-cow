using System.Globalization;
using System.Threading.RateLimiting;
using CacheCow.Modules.WholesaleB2B.RateLimits;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace CacheCow.Host.Security;

/// <summary>
/// Named rate-limit policy classes (SECURITY.md, HTTP boundary rule 7;
/// CC-API-008 enforcement behavior; issue 019). Endpoints attach with
/// RequireRateLimiting(...) / [EnableRateLimiting(...)].
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>Stricter class for authentication endpoints (login, OTP issuance/verification).</summary>
    public const string Authentication = "authentication";

    /// <summary>Stricter class for order-creation endpoints (CC-API-008: 60/minute ratified default).</summary>
    public const string OrderCreation = "order-creation";
}

/// <summary>
/// Configures the ASP.NET Core rate limiter from <see cref="SecurityOptions"/>:
/// a per-client global limiter plus the stricter named policy classes, fixed
/// window (algorithm not fixed by the specs - issue 019, Open Questions),
/// rejecting with 429 + Retry-After and an RFC 9457 problem-details body, and
/// emitting a structured security event per rejection (no credentials or
/// tokens; SECURITY.md, Logging rules 3-4). Partitioning keys on the
/// server-verified authenticated identity where present, falling back to the
/// source IP; anonymous-client keying is an open decision (issue 019).
/// Counters are per-instance in-process; distributed limiter state across pod
/// replicas is likewise an open question (issue 019) and not resolved here.
/// </summary>
internal sealed class RateLimiterOptionsConfigurator : IConfigureOptions<RateLimiterOptions>
{
    private readonly IOptions<SecurityOptions> _security;

    public RateLimiterOptionsConfigurator(IOptions<SecurityOptions> security)
    {
        _security = security;
    }

    public void Configure(RateLimiterOptions options)
    {
        var settings = _security.Value.RateLimiting;

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                "default:" + ClientPartitionKey(context),
                _ => ToFixedWindowOptions(settings.Default)));

        options.AddPolicy(RateLimitPolicies.Authentication, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                "auth:" + ClientPartitionKey(context),
                _ => ToFixedWindowOptions(settings.Authentication)));

        // Order creation is shared by consumer checkout and the B2B API
        // (B2BRateLimitPolicies.OrderCreation names this same policy). B2B
        // principals partition per authenticated client via the module's
        // documented key provider (CC-API-008); everyone else keeps the
        // host's issue-019 keying.
        options.AddPolicy(RateLimitPolicies.OrderCreation, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                "order:" + (B2BRateLimitPartition.KeyFor(context.User) ?? ClientPartitionKey(context)),
                _ => ToFixedWindowOptions(settings.OrderCreation)));

        // The B2B default per-client policy (CC-API-008: 600 requests/minute
        // ratified default, budgets from configuration), partitioned per
        // authenticated B2B client via B2BRateLimitPartition.KeyFor; requests
        // without a B2B client identity fall back to the host's anonymous
        // keying and must not consume any partner's quota (issue 019).
        options.AddPolicy(B2BRateLimitPolicies.Client, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                "b2b:" + (B2BRateLimitPartition.KeyFor(context.User) ?? ClientPartitionKey(context)),
                _ => ToFixedWindowOptions(settings.B2BClient)));

        options.OnRejected = async (rejectedContext, cancellationToken) =>
        {
            var http = rejectedContext.HttpContext;

            // 429 MUST carry Retry-After (SECURITY.md, HTTP boundary rule 7).
            var retryAfterSeconds = rejectedContext.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                : settings.Default.WindowSeconds;
            http.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            http.Response.StatusCode = StatusCodes.Status429TooManyRequests;

            http.RequestServices.GetRequiredService<ISecurityEventLogger>()
                .RateLimitRejected(ClientPartitionKey(http), http.Request.Path);

            // Generic RFC 9457 body: no queue depths, backend identifiers or
            // any internal state (issue 019 AC-06; SECURITY.md, Logging rule 1).
            await http.RequestServices.GetRequiredService<IProblemDetailsService>().TryWriteAsync(
                new ProblemDetailsContext
                {
                    HttpContext = http,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests.",
                    },
                });
        };
    }

    /// <summary>
    /// Per-client partition key: server-verified authenticated identity where
    /// present (never a client-supplied header), else the source IP.
    /// </summary>
    internal static string ClientPartitionKey(HttpContext context) =>
        context.User.Identity is { IsAuthenticated: true, Name: { Length: > 0 } name }
            ? "user:" + name
            : context.Connection.RemoteIpAddress is { } ip
                ? "ip:" + ip
                : "anonymous";

    private static FixedWindowRateLimiterOptions ToFixedWindowOptions(RateLimitPolicySettings settings) =>
        new()
        {
            PermitLimit = settings.PermitLimit,
            Window = TimeSpan.FromSeconds(settings.WindowSeconds),
            QueueLimit = 0,
            AutoReplenishment = true,
        };
}
