using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace CacheCow.Host.Security;

/// <summary>
/// A caller-owned resource for object-level authorization (CC-SEC-007;
/// SECURITY.md, Authentication rule 9). <see cref="OwnerId"/> is the
/// server-derived owner - a consumer identity for orders/addresses or a
/// partner tenant id for B2B/wholesale data - loaded from the data store,
/// never bound from a request (SECURITY.md, Input validation rule 3).
/// </summary>
public interface ITenantOwnedResource
{
    string OwnerId { get; }
}

/// <summary>
/// Requirement that the caller prove owner-or-tenant match against the
/// resource, server-side, on every access (CC-SEC-007). Singleton: policies
/// add <see cref="Instance"/>.
/// </summary>
public sealed class ResourceOwnershipRequirement : IAuthorizationRequirement
{
    public static ResourceOwnershipRequirement Instance { get; } = new();

    private ResourceOwnershipRequirement()
    {
    }
}

/// <summary>
/// Owner-or-tenant match: the resource's owner must equal one of the caller's
/// server-side identity claims - tenant (<see cref="CacheCowClaims.TenantId"/>),
/// name identifier, or name - taken from the validated principal only, never
/// from route/query/body values (SECURITY.md, Input validation rule 3;
/// Authentication rules 8-9). A mismatch is an explicit hard failure that no
/// other handler can override; exceptions anywhere in evaluation are turned
/// into denials by <see cref="FailClosedAuthorizationService"/> (SECURITY.md,
/// Logging rule 2).
/// </summary>
public sealed class ResourceOwnershipHandler : AuthorizationHandler<ResourceOwnershipRequirement, ITenantOwnedResource>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ResourceOwnershipRequirement requirement,
        ITenantOwnedResource resource)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);

        var owner = resource.OwnerId;
        if (!string.IsNullOrEmpty(owner) && CallerIdentifiers(context.User).Contains(owner, StringComparer.Ordinal))
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail(new AuthorizationFailureReason(this, "caller is not the owner or tenant of the requested resource"));
        }

        return Task.CompletedTask;
    }

    private static IEnumerable<string> CallerIdentifiers(ClaimsPrincipal user)
    {
        foreach (var claim in user.Claims)
        {
            if (claim.Type is CacheCowClaims.TenantId or ClaimTypes.NameIdentifier or ClaimTypes.Name)
            {
                yield return claim.Value;
            }
        }
    }
}

/// <summary>
/// Endpoint metadata marking a resource-scoped endpoint: authorization
/// denials for an authenticated caller (wrong tenant, wrong role) answer 404
/// rather than 403, so a response never confirms the existence of a resource
/// the caller cannot access (CC-SEC-007; SECURITY.md, Authentication rule 9;
/// the same 404-not-403 posture CC-MKT-004 hard-requires). Honored by
/// <see cref="NotFoundForUnmatchedRoutesAuthorizationResultHandler"/>.
/// Unauthenticated requests still receive the ordinary 401 challenge.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class ResourceEndpointAttribute : Attribute
{
}

/// <summary>
/// The reusable object-level authorization helper module endpoints use for
/// every caller-owned resource access (CC-SEC-007; SECURITY.md, Authentication
/// rule 9; issue 062). Both inaccessible and nonexistent resources produce the
/// identical bodyless 404 (the status-code pages stage adds the same RFC 9457
/// body to each), so response shape and existence disclosure cannot diverge;
/// the resource body is never written on a denial.
/// </summary>
public static class ResourceAuthorization
{
    /// <summary>
    /// Named policy enforcing <see cref="ResourceOwnershipRequirement"/> for an
    /// authenticated caller. Registered by AddCacheCowSecurity.
    /// </summary>
    public const string OwnerPolicy = "cachecow-resource-owner";

    /// <summary>
    /// Authorizes <paramref name="resource"/> for <paramref name="user"/> and
    /// returns <paramref name="onAuthorized"/>'s result only on success;
    /// a missing resource and a denied resource both return the identical 404.
    /// </summary>
    public static async Task<IResult> RequireOwnedResourceAsync<TResource>(
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        TResource? resource,
        Func<TResource, IResult> onAuthorized,
        string policyName = OwnerPolicy)
        where TResource : class, ITenantOwnedResource
    {
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(onAuthorized);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);

        if (resource is null)
        {
            return Results.NotFound();
        }

        var decision = await authorization.AuthorizeAsync(user, resource, policyName);
        return decision.Succeeded ? onAuthorized(resource) : Results.NotFound();
    }
}
