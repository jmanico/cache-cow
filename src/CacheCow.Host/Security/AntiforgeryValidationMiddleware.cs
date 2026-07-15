using Microsoft.AspNetCore.Antiforgery;

namespace CacheCow.Host.Security;

/// <summary>Shared antiforgery constants for server and clients/tests.</summary>
public static class CacheCowAntiforgery
{
    /// <summary>Request header carrying the antiforgery request token.</summary>
    public const string HeaderName = "X-CSRF-TOKEN";
}

/// <summary>
/// Global antiforgery enforcement - the minimal-API equivalent of registering
/// [AutoValidateAntiforgeryToken] as a global MVC filter (CC-SEC-006;
/// SECURITY.md, Authentication rule 11; issue 061 AC-04/AC-05/AC-06): every
/// state-changing request (non-GET/HEAD/OPTIONS/TRACE) that rides an ambient
/// cookie credential must present a valid antiforgery token; there is no
/// per-endpoint opt-out. Two request classes have no CSRF surface and are
/// exempt, exactly as rule 11 scopes it:
/// (1) bearer-token requests - the Authorization header is attached
/// explicitly by the client and cannot be set by a cross-site forged request;
/// (2) requests carrying no cookies at all - there is no ambient credential
/// to ride.
/// Runs after authorization so authentication/authorization status codes
/// (401/403/404) take precedence, and before the endpoint so a rejected
/// request never executes a state change. Failures answer 400 with the RFC
/// 9457 body supplied by the status-code pages stage and are logged as
/// validation rejections (SECURITY.md, Logging rule 3); any exception in
/// validation is a rejection, never a bypass (Logging rule 2).
/// </summary>
public sealed class AntiforgeryValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAntiforgery _antiforgery;
    private readonly ISecurityEventLogger _events;

    public AntiforgeryValidationMiddleware(RequestDelegate next, IAntiforgery antiforgery, ISecurityEventLogger events)
    {
        _next = next;
        _antiforgery = antiforgery;
        _events = events;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!RequiresValidation(context))
        {
            await _next(context);
            return;
        }

        try
        {
            await _antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            _events.ValidationRejected("antiforgery-token-invalid", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
#pragma warning disable CA1031 // Fail closed on *any* exception in the antiforgery path (SECURITY.md, Logging rule 2).
        catch (Exception)
#pragma warning restore CA1031
        {
            _events.ValidationRejected("antiforgery-validation-error", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        await _next(context);
    }

    private static bool RequiresValidation(HttpContext context)
    {
        var method = context.Request.Method;
        if (HttpMethods.IsGet(method)
            || HttpMethods.IsHead(method)
            || HttpMethods.IsOptions(method)
            || HttpMethods.IsTrace(method))
        {
            return false;
        }

        var authorization = context.Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return context.Request.Cookies.Count > 0;
    }
}
