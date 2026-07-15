using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace CacheCow.Host.Security;

/// <summary>
/// Per-request session validation hook for every cookie authentication scheme
/// (CC-SEC-006; SECURITY.md, Authentication rule 11): the principal is
/// re-validated against <see cref="ISessionRevocation"/> on every request, so
/// a revoked session is treated as unauthenticated on its very next request
/// regardless of cookie lifetime (issue 061 AC-02). Fail closed: a principal
/// without a session id claim, or any exception in the revocation check, is a
/// rejection, never a bypass (SECURITY.md, Logging rule 2). Wired onto all
/// cookie schemes by <see cref="SessionCookieAuthenticationConfigurator"/>;
/// the concrete sign-in flows that stamp the session id claim land with
/// issues 058/060 (Entra wiring) via <see cref="ISessionLifecycle"/>.
/// Challenges answer 401 and forbids 403 (no login-page redirect) until the
/// browser sign-in UX lands with those issues; API-style status codes keep
/// the RFC 9457 error contract (issue 021) intact meanwhile.
/// </summary>
public sealed class RevocationValidatingCookieEvents : CookieAuthenticationEvents
{
    private readonly ISessionRevocation _revocation;
    private readonly ISecurityEventLogger _events;

    public RevocationValidatingCookieEvents(ISessionRevocation revocation, ISecurityEventLogger events)
    {
        _revocation = revocation;
        _events = events;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string reason;
        try
        {
            var sessionId = context.Principal?.FindFirst(CacheCowClaims.SessionId)?.Value;
            if (string.IsNullOrEmpty(sessionId))
            {
                reason = "session-id-missing";
            }
            else if (await _revocation.IsRevokedAsync(sessionId, context.HttpContext.RequestAborted))
            {
                reason = "session-revoked";
            }
            else
            {
                return;
            }
        }
#pragma warning disable CA1031 // Fail closed on *any* exception in the session validation path (SECURITY.md, Logging rule 2).
        catch (Exception)
#pragma warning restore CA1031
        {
            reason = "revocation-check-failed";
        }

        // The reason string is a fixed vocabulary, never the session id itself
        // (session ids are credentials - SECURITY.md, Logging rule 4).
        _events.AuthenticationFailure(context.Principal?.Identity?.Name, reason);
        context.RejectPrincipal();
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
