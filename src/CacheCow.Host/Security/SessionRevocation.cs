using System.Collections.Concurrent;

namespace CacheCow.Host.Security;

/// <summary>
/// Server-side session revocation port (CC-SEC-006; SECURITY.md,
/// Authentication rule 11: "server-side revocation"). A revoked session id is
/// rejected at authentication time by <see cref="RevocationValidatingCookieEvents"/>
/// on the very next request bearing it - revocation never depends on the
/// client deleting its cookie (issue 061 AC-02).
/// </summary>
public interface ISessionRevocation
{
    /// <summary>Revokes the session id; idempotent.</summary>
    ValueTask RevokeAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>True when the session id has been revoked.</summary>
    ValueTask<bool> IsRevokedAsync(string sessionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory revocation store. Single-node only: the production revocation
/// mechanics (shared ticket store vs. IdP-side revocation) are an open
/// decision per issue 061 "Open Questions" and land with the concrete auth
/// wiring (issues 058/060 and the Entra providers). This implementation keeps
/// the port honest and testable until then; entries are retained for the
/// process lifetime, bounded by the session lifetime being itself bounded
/// (SessionCookieSettings.MaxAgeHours).
/// </summary>
public sealed class InMemorySessionRevocation : ISessionRevocation
{
    private readonly ConcurrentDictionary<string, byte> _revoked = new(StringComparer.Ordinal);

    public ValueTask RevokeAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        _revoked[sessionId] = 0;
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> IsRevokedAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return ValueTask.FromResult(_revoked.ContainsKey(sessionId));
    }
}
