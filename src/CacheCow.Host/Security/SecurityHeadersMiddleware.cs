using Microsoft.Extensions.Options;

namespace CacheCow.Host.Security;

/// <summary>
/// Emits the every-response security headers and the strict CSP on HTML
/// responses (SECURITY.md, HTTP boundary rules 2-3; CC-SEC-003):
/// X-Content-Type-Options: nosniff, Referrer-Policy:
/// strict-origin-when-cross-origin and a Permissions-Policy on every response;
/// nonce-based CSP (enforced or Report-Only per configuration) on HTML;
/// Cache-Control: no-store on authenticated and sensitive responses.
/// Registered before static files so no response escapes header enforcement
/// (SECURITY.md, HTTP boundary rule 5).
/// The Permissions-Policy directive set is not enumerated in the specs
/// (issue 017, Open Questions); this is a conservative minimal denial of the
/// common sensitive browser features, pending a human decision.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private const string PermissionsPolicyValue = "camera=(), geolocation=(), microphone=()";

    private readonly RequestDelegate _next;
    private readonly IOptions<SecurityOptions> _options;

    public SecurityHeadersMiddleware(RequestDelegate next, IOptions<SecurityOptions> options)
    {
        _next = next;
        _options = options;
    }

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var nonce = CspNonce.Issue(context);
        context.Response.OnStarting(() =>
        {
            WriteHeaders(context, nonce);
            return Task.CompletedTask;
        });

        return _next(context);
    }

    private void WriteHeaders(HttpContext context, string nonce)
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = PermissionsPolicyValue;

        var csp = _options.Value.Csp;
        var contentType = context.Response.ContentType;
        if (contentType is not null && contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
        {
            headers[ContentSecurityPolicy.HeaderName(csp)] = ContentSecurityPolicy.Build(nonce, csp);
            if (!string.IsNullOrEmpty(csp.ReportEndpoint))
            {
                headers["Reporting-Endpoints"] = $"csp-endpoint=\"{csp.ReportEndpoint}\"";
            }
        }

        var sensitive = context.GetEndpoint()?.Metadata.GetMetadata<SensitiveResponseAttribute>() is not null;
        if (sensitive || context.User.Identity?.IsAuthenticated == true)
        {
            headers.CacheControl = "no-store";
        }
    }
}
