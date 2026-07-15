using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace CacheCow.Host.Security;

/// <summary>
/// Cryptographically random session identifiers: 256 bits from the CSPRNG,
/// base64url-encoded. Session ids are credentials and are never logged
/// (SECURITY.md, Logging rule 4).
/// </summary>
public static class SessionTokens
{
    public static string NewSessionId()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return WebEncoders.Base64UrlEncode(bytes);
    }
}

/// <summary>
/// Session-identifier lifecycle hook contract (CC-SEC-006; SECURITY.md,
/// Authentication rule 11: "session refresh on sign-in and privilege change").
/// The concrete authentication handlers land with issues 058 (consumer)
/// and 060 (staff SSO) plus the Entra wiring; they MUST call
/// <see cref="BeginSession"/> when completing a sign-in (a pre-sign-in
/// session identifier is never carried over - session fixation) and
/// <see cref="RefreshSessionAsync"/> whenever a session's privileges change
/// (role change, step-up), which also revokes the prior identifier so it
/// stops authenticating immediately (issue 061 AC-03).
/// </summary>
public interface ISessionLifecycle
{
    /// <summary>
    /// Stamps a fresh session id claim onto the identity (replacing any
    /// existing one) and returns the new id. Call at sign-in completion.
    /// </summary>
    string BeginSession(ClaimsIdentity identity);

    /// <summary>
    /// Issues a fresh session id claim and revokes the previous one, so the
    /// pre-change session identifier is invalidated server-side. Call on
    /// privilege change before re-issuing the authentication ticket.
    /// </summary>
    ValueTask<string> RefreshSessionAsync(ClaimsIdentity identity, CancellationToken cancellationToken = default);
}

public sealed class SessionLifecycle : ISessionLifecycle
{
    private readonly ISessionRevocation _revocation;

    public SessionLifecycle(ISessionRevocation revocation)
    {
        _revocation = revocation;
    }

    public string BeginSession(ClaimsIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        foreach (var stale in identity.FindAll(CacheCowClaims.SessionId).ToArray())
        {
            identity.RemoveClaim(stale);
        }

        var sessionId = SessionTokens.NewSessionId();
        identity.AddClaim(new Claim(CacheCowClaims.SessionId, sessionId));
        return sessionId;
    }

    public async ValueTask<string> RefreshSessionAsync(ClaimsIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var previous = identity.FindFirst(CacheCowClaims.SessionId)?.Value;
        var next = BeginSession(identity);
        if (!string.IsNullOrEmpty(previous))
        {
            await _revocation.RevokeAsync(previous, cancellationToken);
        }

        return next;
    }
}
