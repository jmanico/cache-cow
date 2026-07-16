namespace CacheCow.Host.Security;

/// <summary>
/// Host security configuration (SECURITY.md, HTTP boundary rules 1-7).
/// Bound from the "Security" configuration section and validated fail-closed
/// at startup by <see cref="SecurityOptionsValidator"/>: if this configuration
/// cannot be loaded or is invalid, the host does not start (SECURITY.md,
/// Logging rule 2; issues 016-019 failure behavior).
/// </summary>
public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public HstsSettings Hsts { get; init; } = new();

    public CspSettings Csp { get; init; } = new();

    public CorsSettings Cors { get; init; } = new();

    public RequestLimitSettings RequestLimits { get; init; } = new();

    public RateLimitingSettings RateLimiting { get; init; } = new();

    public SessionCookieSettings SessionCookies { get; init; } = new();
}

/// <summary>
/// Session cookie policy (CC-SEC-006; SECURITY.md, Authentication rule 11):
/// HttpOnly and Secure are unconditional; SameSite mode and the bounded expiry
/// come from here. Two open decisions per issue 061 (not resolved here):
/// SECURITY.md mandates SameSite without fixing Lax vs. Strict - Strict is the
/// stricter default pending a human decision, and it interacts with
/// payment-processor redirect returns (Stripe/Razorpay, CC-ORD-004); and only
/// the dashboard session lifetime is ratified (12 hours, CC-DSH-001) -
/// consumer storefront and wholesale-portal lifetimes are undecided, so 12
/// hours is the conservative placeholder for all surfaces.
/// </summary>
public sealed class SessionCookieSettings
{
    /// <summary>"Strict" or "Lax"; "None" is rejected at startup.</summary>
    public string SameSite { get; set; } = "Strict";

    /// <summary>Absolute session bound in hours (cookie Max-Age and ticket lifetime).</summary>
    public int MaxAgeHours { get; set; } = 12;

    /// <summary>Fail-closed parse: anything but an explicit "Lax" is Strict.</summary>
    public SameSiteMode ParsedSameSite =>
        string.Equals(SameSite, "Lax", StringComparison.OrdinalIgnoreCase) ? SameSiteMode.Lax : SameSiteMode.Strict;
}

/// <summary>
/// HSTS with preload (SECURITY.md, HTTP boundary rule 1; CC-SEC-003).
/// The concrete max-age value and preload-list submission timing are an open
/// item per issue 016; 365 days is the minimum the browser preload list
/// accepts and is a placeholder, not a ratified decision.
/// </summary>
public sealed class HstsSettings
{
    public int MaxAgeDays { get; set; } = 365;

    public bool IncludeSubDomains { get; set; } = true;

    public bool Preload { get; set; } = true;
}

/// <summary>
/// Strict CSP settings (SECURITY.md, HTTP boundary rule 2; CC-SEC-003).
/// Ships with <see cref="ReportOnly"/> = true: rollout is Report-Only before
/// enforcement per the rule; flipping to enforcement is an operational
/// decision after the Report-Only phase (issue 017, AC-03).
/// </summary>
public sealed class CspSettings
{
    public bool ReportOnly { get; set; } = true;

    /// <summary>
    /// CSP violation-report collection endpoint (report-uri / Reporting-Endpoints).
    /// Empty means no reporting directive is emitted. The concrete endpoint and
    /// its ingestion pipeline are owned by the observability work, not this host.
    /// </summary>
    public string? ReportEndpoint { get; set; }

    public PaymentProcessorOrigins PaymentProcessorOrigins { get; init; } = new();
}

/// <summary>
/// Exact payment-processor origins allowlisted into form-action, frame-src and
/// connect-src (SECURITY.md, HTTP boundary rule 2). Specific origins only -
/// wildcards and suffix matches are rejected at startup. The concrete origin
/// lists for Stripe, Razorpay, PayPal, SEPA, konbini and UPI are NOT specified
/// in the canonical documents; they are supplied by the payment-integration
/// issues (039/040) from processor documentation and human-reviewed. Shipped
/// empty deliberately (issue 017, Open Questions).
/// </summary>
public sealed class PaymentProcessorOrigins
{
    public IList<string> FormAction { get; } = [];

    public IList<string> FrameSrc { get; } = [];

    public IList<string> ConnectSrc { get; } = [];
}

/// <summary>
/// CORS allowlist (SECURITY.md, HTTP boundary rule 4): explicit exact origins
/// only, never credentials with wildcard or suffix-matched origins. Shipped
/// empty (default deny): the legitimate cross-origin topology is not yet
/// documented (issue 018, Open Questions).
/// </summary>
public sealed class CorsSettings
{
    public IList<string> AllowedOrigins { get; } = [];

    public bool AllowCredentials { get; set; }
}

/// <summary>
/// Request body and paging caps (SECURITY.md, HTTP boundary rule 7; CC-CNT-004).
/// Concrete numeric values are not specified in the canonical documents
/// (issue 018, Open Questions); defaults here are conservative placeholders
/// pending a human/engineering decision.
/// </summary>
public sealed class RequestLimitSettings
{
    public long MaxRequestBodyBytes { get; set; } = 1_048_576;

    public int DefaultPageSize { get; set; } = 20;

    public int MaxPageSize { get; set; } = 100;
}

/// <summary>
/// Rate-limit policy classes (SECURITY.md, HTTP boundary rule 7; CC-API-008).
/// Default and OrderCreation encode the ratified CC-API-008 numbers
/// (600 requests/minute; 60 order-creations/minute). Per-partner tiers are
/// applied by issue 056. The Authentication class limit is NOT specified in
/// the specs (issue 019, Open Questions); 10/minute is a conservative
/// placeholder pending a human decision.
/// </summary>
public sealed class RateLimitingSettings
{
    public RateLimitPolicySettings Default { get; init; } = new() { PermitLimit = 600, WindowSeconds = 60 };

    public RateLimitPolicySettings Authentication { get; init; } = new() { PermitLimit = 10, WindowSeconds = 60 };

    public RateLimitPolicySettings OrderCreation { get; init; } = new() { PermitLimit = 60, WindowSeconds = 60 };

    /// <summary>
    /// The B2B per-client default policy ("b2b-client", CC-API-008: 600
    /// requests/minute ratified 2026-07-15), partitioned per authenticated
    /// B2B client. Per-partner tier overrides (IB2BRateLimitTierSource) await
    /// the partner-resolution wiring of the Entra client directory (issue 056
    /// note in the composition root).
    /// </summary>
    public RateLimitPolicySettings B2BClient { get; init; } = new() { PermitLimit = 600, WindowSeconds = 60 };
}

public sealed class RateLimitPolicySettings
{
    public int PermitLimit { get; set; } = 100;

    public int WindowSeconds { get; set; } = 60;
}
