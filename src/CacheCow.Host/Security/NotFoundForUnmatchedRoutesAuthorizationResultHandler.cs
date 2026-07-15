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
/// Additionally (issue 062, CC-SEC-007): endpoints marked with
/// <see cref="ResourceEndpointAttribute"/> map an authenticated caller's
/// authorization *forbid* (wrong tenant, wrong role) to the same 404, so a
/// role- or tenancy-denied resource is indistinguishable from a nonexistent
/// one. Unauthenticated requests to matched endpoints keep the 401 challenge,
/// and unmarked endpoints keep the framework's 403, so real endpoints stay
/// deny-by-default.
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

        if (!authorizeResult.Succeeded)
        {
            var endpoint = context.GetEndpoint();
            if (endpoint is null
                || (authorizeResult.Forbidden && endpoint.Metadata.GetMetadata<ResourceEndpointAttribute>() is not null))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            }
        }

        return _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
