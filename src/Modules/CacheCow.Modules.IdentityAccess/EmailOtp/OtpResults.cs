namespace CacheCow.Modules.IdentityAccess.EmailOtp;

public enum OtpIssuanceOutcome
{
    /// <summary>Request accepted — returned identically for known and unknown addresses.</summary>
    Accepted,

    /// <summary>Per-account or per-IP issuance limit exceeded; maps to 429 + Retry-After.</summary>
    RateLimited,
}

/// <summary>
/// Issuance outcome. <see cref="Accepted"/> is a singleton so the result for
/// a known address and an unknown address is literally the same object —
/// byte-identical at any serialization boundary (CC-SEC-016; issue 059, AC-06).
/// </summary>
public sealed class OtpIssuanceResult
{
    private OtpIssuanceResult(OtpIssuanceOutcome outcome, TimeSpan? retryAfter)
    {
        Outcome = outcome;
        RetryAfter = retryAfter;
    }

    public static OtpIssuanceResult Accepted { get; } = new(OtpIssuanceOutcome.Accepted, null);

    public static OtpIssuanceResult RateLimited(TimeSpan retryAfter) =>
        new(OtpIssuanceOutcome.RateLimited, retryAfter);

    public OtpIssuanceOutcome Outcome { get; }

    /// <summary>Retry-After value when rate limited; null otherwise.</summary>
    public TimeSpan? RetryAfter { get; }
}

public enum OtpVerificationOutcome
{
    /// <summary>The single outstanding, unexpired code for an existing account matched; it is now consumed.</summary>
    Verified,

    /// <summary>
    /// Generic denial: wrong code, no outstanding code, expired, superseded,
    /// or no such account — deliberately one indistinguishable outcome
    /// (CC-SEC-016; issue 059, AC-06 and Failure Behavior). The HTTP layer
    /// maps it to one generic RFC 9457 response.
    /// </summary>
    Failed,

    /// <summary>Throttled or locked out; maps to 429 + Retry-After (issue 059, AC-04).</summary>
    RateLimited,
}

/// <summary>
/// Verification outcome. <see cref="Verified"/> and <see cref="Failed"/> are
/// singletons: every failure reason returns the same object, so no field can
/// leak why verification failed or whether the account exists.
/// </summary>
public sealed class OtpVerificationResult
{
    private OtpVerificationResult(OtpVerificationOutcome outcome, TimeSpan? retryAfter)
    {
        Outcome = outcome;
        RetryAfter = retryAfter;
    }

    public static OtpVerificationResult Verified { get; } = new(OtpVerificationOutcome.Verified, null);

    public static OtpVerificationResult Failed { get; } = new(OtpVerificationOutcome.Failed, null);

    public static OtpVerificationResult RateLimited(TimeSpan retryAfter) =>
        new(OtpVerificationOutcome.RateLimited, retryAfter);

    public OtpVerificationOutcome Outcome { get; }

    /// <summary>Retry-After value when rate limited or locked out; null otherwise.</summary>
    public TimeSpan? RetryAfter { get; }
}
