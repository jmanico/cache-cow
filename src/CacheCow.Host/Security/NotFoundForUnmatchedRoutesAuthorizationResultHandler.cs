using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace CacheCow.Host.Security;

/// <summary>
/// The deny-by-default fallback policy (SECURITY.md, Authentication rule 1;
/// issue 020) also covers requests that match no endpoint at all. For those,
/// a 401 challenge would confirm nothing exists to sign in *for*; a
/// nonexistent path answers 404 instead - the derived hardening default for
/// inaccessible resources of SECURITY.md, Authentication rule 9 (and the
/// status semantics CC-MKT-004 hard-requires for gated IN resources later).
/// Requests that DID match an endpoint keep the framework's denial behavior
/// (401 challenge / 403 forbid), so real endpoints stay deny-by-default.
/// </summary>
public sealed class NotFoundForUnmatchedRoutesAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(authorizeResult);

        if (!authorizeResult.Succeeded && context.GetEndpoint() is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }

        return _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
