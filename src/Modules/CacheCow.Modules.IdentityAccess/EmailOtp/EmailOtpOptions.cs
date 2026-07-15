namespace CacheCow.Modules.IdentityAccess.EmailOtp;

/// <summary>
/// Required email-OTP configuration (SECURITY.md, Authentication and
/// authorization rule 13; CC-SEC-016). Two values are hard-capped by the
/// requirement itself and validated here: code length MUST be at least
/// 6 digits and code lifetime MUST NOT exceed 10 minutes.
///
/// OPEN DECISION (issue 059, Open Questions): the exact numeric thresholds
/// for "a small number of failed attempts", the backoff/lockout duration,
/// and the per-account/per-IP issuance and verification rate limits are
/// specified nowhere in SECURITY.md rule 13. They therefore have NO defaults
/// and no parameterless constructor — the host must supply human-ratified
/// values; this type only enforces the bounds the spec does fix.
/// </summary>
public sealed class EmailOtpOptions
{
    /// <summary>Spec floor: at least 6 digits of cryptographic-RNG entropy (CC-SEC-016).</summary>
    public const int MinimumCodeLength = 6;

    /// <summary>Engineering sanity bound for generated digit strings; not a spec value.</summary>
    public const int MaximumCodeLength = 32;

    /// <summary>Spec ceiling: OTP expiry MUST be at most 10 minutes (CC-SEC-016).</summary>
    public static readonly TimeSpan MaximumCodeLifetime = TimeSpan.FromMinutes(10);

    public EmailOtpOptions(
        int codeLength,
        TimeSpan codeLifetime,
        int maxVerificationFailures,
        TimeSpan lockoutDuration,
        TimeSpan rateLimitWindow,
        int issuanceLimitPerAccount,
        int issuanceLimitPerIp,
        int verificationLimitPerAccount,
        int verificationLimitPerIp)
    {
        if (codeLength is < MinimumCodeLength or > MaximumCodeLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(codeLength),
                codeLength,
                "OTP code length must be at least 6 digits (CC-SEC-016) and at most 32.");
        }

        if (codeLifetime <= TimeSpan.Zero || codeLifetime > MaximumCodeLifetime)
        {
            throw new ArgumentOutOfRangeException(
                nameof(codeLifetime),
                codeLifetime,
                "OTP code lifetime must be positive and at most 10 minutes (CC-SEC-016).");
        }

        if (maxVerificationFailures < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxVerificationFailures),
                maxVerificationFailures,
                "Max verification failures must be at least 1 (value awaits human ratification; issue 059, Open Questions).");
        }

        if (lockoutDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lockoutDuration),
                lockoutDuration,
                "Lockout duration must be positive (value awaits human ratification; issue 059, Open Questions).");
        }

        if (rateLimitWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rateLimitWindow),
                rateLimitWindow,
                "Rate-limit window must be positive (value awaits human ratification; issue 059, Open Questions).");
        }

        ValidateLimit(issuanceLimitPerAccount, nameof(issuanceLimitPerAccount));
        ValidateLimit(issuanceLimitPerIp, nameof(issuanceLimitPerIp));
        ValidateLimit(verificationLimitPerAccount, nameof(verificationLimitPerAccount));
        ValidateLimit(verificationLimitPerIp, nameof(verificationLimitPerIp));

        CodeLength = codeLength;
        CodeLifetime = codeLifetime;
        MaxVerificationFailures = maxVerificationFailures;
        LockoutDuration = lockoutDuration;
        RateLimitWindow = rateLimitWindow;
        IssuanceLimitPerAccount = issuanceLimitPerAccount;
        IssuanceLimitPerIp = issuanceLimitPerIp;
        VerificationLimitPerAccount = verificationLimitPerAccount;
        VerificationLimitPerIp = verificationLimitPerIp;
    }

    /// <summary>Digits per code; at least 6 (CC-SEC-016).</summary>
    public int CodeLength { get; }

    /// <summary>How long an issued code can verify; at most 10 minutes (CC-SEC-016).</summary>
    public TimeSpan CodeLifetime { get; }

    /// <summary>Failed verification attempts against one outstanding code before lockout.</summary>
    public int MaxVerificationFailures { get; }

    /// <summary>How long verification for an account key is refused after lockout.</summary>
    public TimeSpan LockoutDuration { get; }

    /// <summary>Fixed window over which the issuance/verification limits below apply.</summary>
    public TimeSpan RateLimitWindow { get; }

    /// <summary>Issuance requests allowed per account key per window.</summary>
    public int IssuanceLimitPerAccount { get; }

    /// <summary>Issuance requests allowed per client address per window.</summary>
    public int IssuanceLimitPerIp { get; }

    /// <summary>Verification attempts allowed per account key per window.</summary>
    public int VerificationLimitPerAccount { get; }

    /// <summary>Verification attempts allowed per client address per window.</summary>
    public int VerificationLimitPerIp { get; }

    private static void ValidateLimit(int value, string paramName)
    {
        if (value < 1)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                "Rate limits must be at least 1 per window (values await human ratification; issue 059, Open Questions).");
        }
    }
}
