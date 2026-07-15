namespace CacheCow.Host.Security;

/// <summary>
/// Composes the host middleware pipeline in the order fixed by SECURITY.md,
/// HTTP boundary rule 5: HTTPS and security headers before static files, then
/// authentication, then authorization (issue 016 AC-04/AC-05/AC-07).
/// <see cref="CanonicalOrder"/> is the single source of truth for the order;
/// <see cref="UseCacheCowSecurityPipeline"/> registers each stage top to
/// bottom, and the ordering test suite asserts the rule-5 invariants against
/// this list plus the observable pipeline behavior. Any edit that reorders
/// registration MUST update the list, which fails the ordering tests on a
/// rule-5 regression.
/// </summary>
public static class SecurityPipeline
{
    /// <summary>Pipeline stages, outermost first, exactly as registered.</summary>
    public static readonly IReadOnlyList<string> CanonicalOrder =
    [
        "ExceptionHandling",
        "StatusCodePages",
        "HttpsRedirection",
        "Hsts",
        "SecurityHeaders",
        "ValidationRejectionLogging",
        "RequestBodySizeLimit",
        "CookiePolicy",
        "StaticFiles",
        "Routing",
        "Cors",
        "Authentication",
        "RateLimiter",
        "Authorization",
        "AntiforgeryValidation",
    ];

    public static WebApplication UseCacheCowSecurityPipeline(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // ExceptionHandling: developer exception page in Development ONLY
        // (SECURITY.md, Logging rule 1; issue 021 AC-02); everywhere else the
        // global handler serializes RFC 9457 problem details with no internal
        // detail.
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler();
        }

        // StatusCodePages: bodyless rejections (401/404/405/413/415/429...)
        // gain RFC 9457 problem-details bodies (CC-API-006; issue 021).
        app.UseStatusCodePages();

        // HttpsRedirection + Hsts: browser-facing transport enforcement
        // (SECURITY.md, HTTP boundary rule 1; issue 016 AC-01). API hosts
        // additionally never bind a plaintext listener - that is Kestrel
        // endpoint/ingress configuration, not middleware; see Program.cs.
        app.UseHttpsRedirection();
        app.UseHsts();

        // SecurityHeaders before static files so no response escapes header
        // enforcement (SECURITY.md, HTTP boundary rules 2-3 and 5).
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<ValidationRejectionLoggingMiddleware>();
        app.UseMiddleware<RequestBodySizeLimitMiddleware>();

        // CookiePolicy: no response cookie leaves the host without
        // HttpOnly/Secure/SameSite, wherever it is appended downstream
        // (CC-SEC-006; issue 061 AC-01 backstop).
        app.UseCookiePolicy();

        app.UseStaticFiles();

        app.UseRouting();

        // CORS after routing, before authentication (standard ASP.NET Core
        // ordering within the rule-5 frame; issue 018).
        app.UseCors(CorsOptionsConfigurator.PolicyName);

        // Authentication strictly before authorization (SECURITY.md, HTTP
        // boundary rule 5). The rate limiter sits between them so per-client
        // partitions can key on the server-verified authenticated identity
        // (issue 019) while still rejecting before any handler runs.
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();

        // AntiforgeryValidation after authorization (401/403/404 semantics
        // take precedence) and before the endpoint, so a cookie-authenticated
        // state-changing request without a valid token is rejected before any
        // state change executes (CC-SEC-006; issue 061 AC-04/AC-05).
        app.UseMiddleware<AntiforgeryValidationMiddleware>();

        return app;
    }
}
