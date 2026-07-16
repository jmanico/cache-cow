using CacheCow.Modules.ContentLocalization.Email;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CacheCow.Modules.ContentLocalization.Contact;

/// <summary>
/// The contact-submission endpoint (CC-CNT-004; SECURITY.md, Input validation
/// rule 10): POST only, strict JSON binding to a dedicated DTO,
/// reject-not-sanitize server-side validation, honeypot and minimum-fill-time
/// heuristics, the CAPTCHA-equivalent challenge port (fail closed while the
/// mechanism is undecided), and dispatch of the composed notification —
/// user input never in SMTP headers — to the config-set internal recipient.
///
/// HOST WIRING (the host calls <see cref="MapContactEndpoints"/>; this module
/// never maps itself):
/// <list type="bullet">
/// <item>Register a <see cref="ContactFormOptions"/> instance (recipient +
/// minimum fill time). Absent, the endpoint answers 503 and processes
/// nothing.</item>
/// <item>Replace <see cref="IAbuseChallengeVerifier"/> once the
/// CAPTCHA-equivalent mechanism is ratified; the default denies every
/// submission.</item>
/// <item>Register a rate-limiter policy named
/// <see cref="ContactRateLimitPolicies.ContactForm"/> (issue 019 middleware;
/// 429 + Retry-After); the numeric budget is unratified host
/// configuration.</item>
/// <item>Body-size caps, security headers, and RFC 9457 exception shaping are
/// host middleware (issues 016–021).</item>
/// <item>The route is anonymous by design (public form): if the storefront
/// gains a cookie session at this route, antiforgery applies per SECURITY.md,
/// Authentication rule 11 (issue 061 decides the session posture).</item>
/// </list>
/// </summary>
public static class ContactEndpoints
{
    public const string Path = "/contact";

    /// <summary>Bound applied to the opaque challenge response before the verifier sees it.</summary>
    public const int MaxChallengeResponseLength = 8192;

    public static IEndpointRouteBuilder MapContactEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // POST only: every other method on this route is 405 via endpoint
        // routing (SECURITY.md, HTTP boundary rule 6). AllowAnonymous is the
        // explicit opt-out from the host's deny-by-default fallback policy
        // (SECURITY.md, Authentication rule 1) — guest visitors have no
        // account (CC-ORD-001) and the form is public by requirement.
        endpoints.MapPost(Path, (Delegate)SubmitAsync)
            .AllowAnonymous()
            .RequireRateLimiting(ContactRateLimitPolicies.ContactForm);

        return endpoints;
    }

    private static async Task<IResult> SubmitAsync(HttpContext http)
    {
        var logger = http.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("CacheCow.ContentLocalization.Contact");

        // Submission responses are per-user and sensitive: never cacheable
        // (SECURITY.md, HTTP boundary rule 3).
        http.Response.Headers.CacheControl = "no-store";

        // Fail closed until the host supplies the open-decision configuration.
        var options = http.RequestServices.GetService<ContactFormOptions>();
        if (options is null)
        {
            ContactSecurityEvents.NotConfigured(logger);
            return ContactProblems.Unavailable();
        }

        var (request, rejection) = await ContactRequestReader.ReadAsync<ContactSubmissionRequest>(http);
        if (rejection is not null || request is null)
        {
            ContactSecurityEvents.Rejected(logger, "schema");
            return rejection ?? ContactProblems.Rejected();
        }

        // First-party heuristics (SECURITY.md, Input validation rule 10):
        // a filled honeypot or an implausibly fast fill marks automation.
        // Rejected without processing; same generic body as every rejection.
        if (!string.IsNullOrEmpty(request.Website))
        {
            ContactSecurityEvents.AbuseRejected(logger, "honeypot");
            return ContactProblems.Rejected();
        }

        if (request.FillTimeMs is not { } fillTimeMs || fillTimeMs < options.MinimumFillMilliseconds)
        {
            ContactSecurityEvents.AbuseRejected(logger, "fill-time");
            return ContactProblems.Rejected();
        }

        if (request.ChallengeResponse is { } challenge
            && (challenge.Length > MaxChallengeResponseLength || challenge.Any(char.IsControl)))
        {
            ContactSecurityEvents.Rejected(logger, "challenge-shape");
            return ContactProblems.Rejected();
        }

        // Schema validation: reject, never sanitize (CC-CNT-004, CC-SEC-001).
        // The logged reason is the enum constant — no submitted value ever
        // enters a log entry (SECURITY.md, Logging rules 4-5).
        if (!ContactSubmission.TryCreate(
                request.Name, request.Email, request.Topic, request.Message,
                out var submission, out var reason))
        {
            ContactSecurityEvents.Rejected(logger, reason.ToString());
            return ContactProblems.Rejected();
        }

        // CAPTCHA-equivalent control: mechanism undecided (issue 076), so the
        // module default denies everything; a verifier fault is a denial,
        // never a bypass (SECURITY.md, Logging rule 2).
        AbuseChallengeDecision decision;
        try
        {
            decision = await http.RequestServices
                .GetRequiredService<IAbuseChallengeVerifier>()
                .VerifyAsync(new AbuseChallengeEvidence(request.ChallengeResponse), http.RequestAborted);
        }
        catch (Exception)
        {
            decision = AbuseChallengeDecision.Denied;
        }

        if (decision != AbuseChallengeDecision.Allowed)
        {
            ContactSecurityEvents.AbuseRejected(logger, "challenge");
            return ContactProblems.Rejected();
        }

        // Headers come from the composition type (Content-Language only);
        // the recipient is server configuration. No user byte can reach
        // either (SECURITY.md, Input validation rule 10).
        var notification = ContactNotificationComposer.Compose(submission!);
        await http.RequestServices
            .GetRequiredService<IEmailDispatch>()
            .DispatchAsync(notification, options.InternalRecipientEmailAddress, http.RequestAborted);

        ContactSecurityEvents.Accepted(logger);
        return Results.Ok();
    }
}

/// <summary>
/// Structured security events for the contact endpoint (SECURITY.md, Logging
/// rules 3-5; CC-SEC-010): templates only, never interpolation; every
/// parameter is a server-controlled constant (enum name or literal), so no
/// user-supplied value — and therefore no log-injection or PII surface — can
/// enter an entry. Spike alerting on these events is centralized-monitoring
/// scope (issue 022).
/// </summary>
internal static partial class ContactSecurityEvents
{
    [LoggerMessage(EventId = 7601, Level = LogLevel.Error,
        Message = "Contact endpoint invoked without ContactFormOptions; failing closed with 503 (CC-CNT-004)")]
    internal static partial void NotConfigured(ILogger logger);

    [LoggerMessage(EventId = 7602, Level = LogLevel.Warning,
        Message = "Contact submission rejected by validation: {Reason} (CC-CNT-004, CC-SEC-001)")]
    internal static partial void Rejected(ILogger logger, string reason);

    [LoggerMessage(EventId = 7603, Level = LogLevel.Warning,
        Message = "Contact submission rejected by abuse control: {Control} (CC-CNT-004)")]
    internal static partial void AbuseRejected(ILogger logger, string control);

    [LoggerMessage(EventId = 7604, Level = LogLevel.Information,
        Message = "Contact submission accepted and forwarded to the configured internal recipient (CC-CNT-004)")]
    internal static partial void Accepted(ILogger logger);
}
