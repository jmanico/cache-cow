using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace CacheCow.Host.Security;

/// <summary>
/// Enforces the configured request-body cap (SECURITY.md, HTTP boundary
/// rule 7; CC-CNT-004; issue 018 AC-05). Kestrel's MaxRequestBodySize is the
/// production transport-level ceiling (set in Program); this middleware also
/// applies the cap via the per-request feature and rejects declared oversize
/// bodies with 413 before any handler or model binding runs - which keeps the
/// behavior observable in-process where the test server bypasses Kestrel.
/// The 413 body is an RFC 9457 problem-details response via the status-code
/// pages middleware (issue 021). Tighter per-route caps use [RequestSizeLimit].
/// </summary>
public sealed class RequestBodySizeLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptions<SecurityOptions> _options;
    private readonly ISecurityEventLogger _events;

    public RequestBodySizeLimitMiddleware(RequestDelegate next, IOptions<SecurityOptions> options, ISecurityEventLogger events)
    {
        _next = next;
        _options = options;
        _events = events;
    }

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var max = _options.Value.RequestLimits.MaxRequestBodyBytes;

        var sizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeFeature is { IsReadOnly: false })
        {
            sizeFeature.MaxRequestBodySize = max;
        }

        if (context.Request.ContentLength > max)
        {
            _events.ValidationRejected("request-body-size", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return Task.CompletedTask;
        }

        return _next(context);
    }
}
