namespace CacheCow.Modules.BackOffice.Rbac;

/// <summary>Why a dashboard permission check denied (CC-DSH-002).</summary>
public enum AccessDenialReason
{
    /// <summary>Not denied — the check granted.</summary>
    None = 0,

    /// <summary>No role–permission matrix is configured; the required human-authored content is missing (issue 080, Open Questions).</summary>
    MatrixNotConfigured,

    /// <summary>The session's role claim does not resolve against the closed role set.</summary>
    UnknownRole,

    /// <summary>The matrix does not grant this permission to this role (least privilege; no implicit grants).</summary>
    PermissionNotGranted,

    /// <summary>The permission is sensitive but no step-up max-age policy is configured (required, unratified configuration).</summary>
    StepUpPolicyNotConfigured,

    /// <summary>The permission is sensitive and no re-authentication has occurred in this session (SECURITY.md, Authentication rule 2).</summary>
    ReauthenticationMissing,

    /// <summary>The permission is sensitive and the last re-authentication is stale (or implausibly in the future) relative to the configured max age.</summary>
    ReauthenticationStale,

    /// <summary>An exception occurred inside the authorization path; failure is a denial, never a bypass (SECURITY.md, Logging rule 2).</summary>
    AuthorizationFault,
}

/// <summary>
/// The outcome of a dashboard permission check. Denials carry a reason for
/// the structured authz-denial security event the caller logs (issue 080,
/// AC-03; SECURITY.md, Logging rule 3); the client-facing error stays generic
/// (SECURITY.md, Logging rule 1).
/// </summary>
public sealed class AccessDecision
{
    /// <summary>The single granted decision.</summary>
    public static readonly AccessDecision Granted = new(isGranted: true, AccessDenialReason.None);

    private AccessDecision(bool isGranted, AccessDenialReason denial)
    {
        IsGranted = isGranted;
        Denial = denial;
    }

    public bool IsGranted { get; }

    /// <summary><see cref="AccessDenialReason.None"/> when granted; otherwise the denial reason.</summary>
    public AccessDenialReason Denial { get; }

    public static AccessDecision Deny(AccessDenialReason reason)
    {
        if (reason == AccessDenialReason.None)
        {
            throw new ArgumentOutOfRangeException(nameof(reason), "A denial requires a reason other than None.");
        }

        return new AccessDecision(isGranted: false, reason);
    }
}
