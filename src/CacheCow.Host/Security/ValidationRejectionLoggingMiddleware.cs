namespace CacheCow.Host.Security;

/// <summary>
/// Emits a structured validation-rejection security event for HTTP-boundary
/// rejections (405 method not allowed, 415 unsupported media type, 400 bad
/// request) so they stream to centralized monitoring (SECURITY.md, Logging
/// rule 3; issue 018 AC-07). 413 is logged at its enforcement point in
/// <see cref="RequestBodySizeLimitMiddleware"/>; 429 at the rate limiter's
/// rejection callback.
/// </summary>
public sealed class ValidationRejectionLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ISecurityEventLogger _events;

    public ValidationRejectionLoggingMiddleware(RequestDelegate next, ISecurityEventLogger events)
    {
        _next = next;
        _events = events;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        await _next(context);

        switch (context.Response.StatusCode)
        {
            case StatusCodes.Status405MethodNotAllowed:
                _events.ValidationRejected("http-method-not-allowed", context.Request.Path);
                break;
            case StatusCodes.Status415UnsupportedMediaType:
                _events.ValidationRejected("unsupported-media-type", context.Request.Path);
                break;
            case StatusCodes.Status400BadRequest:
                _events.ValidationRejected("bad-request", context.Request.Path);
                break;
            default:
                break;
        }
    }
}
