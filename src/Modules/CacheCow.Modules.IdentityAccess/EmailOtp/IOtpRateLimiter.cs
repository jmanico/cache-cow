namespace CacheCow.Modules.IdentityAccess.EmailOtp;

/// <summary>Outcome of a rate-limit acquisition; maps to 429 + Retry-After at the HTTP layer.</summary>
/// <param name="Allowed">Whether the request may proceed.</param>
/// <param name="RetryAfter">How long the caller must wait when denied; zero when allowed.</param>
public readonly record struct OtpRateLimitDecision(bool Allowed, TimeSpan RetryAfter);

/// <summary>
/// Port for OTP issuance/verification rate-limit state (CC-SEC-016;
/// SECURITY.md, HTTP boundary rule 7 — stricter limits on auth endpoints).
/// The service acquires per-account and per-IP scopes on BOTH issuance and
/// verification. State lives behind this port so a distributed implementation
/// can replace the in-memory default.
/// </summary>
public interface IOtpRateLimiter
{
    /// <summary>
    /// Counts one request against <paramref name="scopeKey"/> and reports
    /// whether it stays within <paramref name="limit"/> per
    /// <paramref name="window"/>.
    /// </summary>
    OtpRateLimitDecision Acquire(string scopeKey, int limit, TimeSpan window);
}
