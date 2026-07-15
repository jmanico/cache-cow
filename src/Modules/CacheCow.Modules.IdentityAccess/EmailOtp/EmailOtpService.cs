namespace CacheCow.Modules.IdentityAccess.EmailOtp;

/// <summary>
/// Email-code (OTP) issuance and verification, hardened per SECURITY.md,
/// Authentication and authorization rule 13 (CC-SEC-016; issue 059). This is
/// the self-contained hardening component the consumer sign-in flow (Entra
/// External ID integration, issue 058 — or a first-party flow) plugs into;
/// all cross-context needs (identity directory, email delivery, durable
/// state) are ports.
///
/// Timing-equalization approach (issue 059, AC-06): known and unknown
/// addresses share one code path end to end.
/// <list type="bullet">
/// <item>Both are keyed by the same SHA-256 pseudonymous account key and pass
/// through the same per-account and per-IP limiter acquisitions (both scopes
/// are always acquired — no short-circuit between them).</item>
/// <item>Issuance does identical work for both populations: generate, salt,
/// digest, store a real record (unknown addresses get a record flagged
/// <c>AccountExists=false</c> that can never verify), invoke the dispatch
/// port, return the same singleton result. Whether mail is actually sent is
/// the adapter's decision, behind the port.</item>
/// <item>Verification never consults the account directory: existence was
/// baked into the stored record at issuance, so there is no known/unknown
/// branch. Every submission — any length, any content — is HMAC-digested and
/// compared with <c>CryptographicOperations.FixedTimeEquals</c> against a
/// fixed 32-byte digest; when no record is outstanding a pre-built decoy
/// record keeps the digest-and-compare work identical. Success requires
/// match, record presence, account existence, and non-expiry combined with
/// non-short-circuiting <c>&amp;</c>, and every failure reason returns the
/// same singleton <see cref="OtpVerificationResult.Failed"/>.</item>
/// </list>
/// Fail-closed: no exception path returns success — any throw in issuance or
/// verification is a denial (SECURITY.md, Logging rule 2).
/// </summary>
public sealed class EmailOtpService
{
    private readonly IOtpStore _store;
    private readonly IOtpRateLimiter _rateLimiter;
    private readonly IAccountDirectory _accountDirectory;
    private readonly IOtpDispatcher _dispatcher;
    private readonly EmailOtpOptions _options;
    private readonly IOtpSecurityEventSink _events;
    private readonly TimeProvider _timeProvider;

    // Random digest that matches no submission; keeps the digest-and-compare
    // path uniform when no code is outstanding for the account key.
    private readonly OtpRecord _decoy;

    public EmailOtpService(
        IOtpStore store,
        IOtpRateLimiter rateLimiter,
        IAccountDirectory accountDirectory,
        IOtpDispatcher dispatcher,
        EmailOtpOptions options,
        IOtpSecurityEventSink? events = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(rateLimiter);
        ArgumentNullException.ThrowIfNull(accountDirectory);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(options);

        _store = store;
        _rateLimiter = rateLimiter;
        _accountDirectory = accountDirectory;
        _dispatcher = dispatcher;
        _options = options;
        _events = events ?? new LoggerOtpSecurityEventSink(null);
        _timeProvider = timeProvider ?? TimeProvider.System;

        _decoy = new OtpRecord(
            accountKey: "decoy",
            salt: OtpCodeHasher.NewSalt(),
            digest: OtpCodeHasher.NewSalt(), // random 32 bytes; matches nothing
            issuedAt: DateTimeOffset.MinValue,
            expiresAt: DateTimeOffset.MinValue + TimeSpan.FromTicks(1),
            accountExists: false,
            failedAttempts: 0);
    }

    /// <summary>
    /// Issues a fresh code for the address and hands it to the dispatch port.
    /// Returns the identical <see cref="OtpIssuanceResult.Accepted"/> whether
    /// or not the address maps to an account (CC-SEC-016). Issuing supersedes
    /// any outstanding code for the same address (AC-03).
    /// </summary>
    public async Task<OtpIssuanceResult> RequestCodeAsync(
        string emailAddress,
        string clientAddress,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientAddress);

        var accountKey = OtpAccountKey.Derive(emailAddress);

        // Both scopes are always acquired (rule 13: limits on issuance,
        // per-account AND per-IP); | avoids short-circuiting the second.
        var ipDecision = _rateLimiter.Acquire(
            $"otp:issue:ip:{clientAddress}", _options.IssuanceLimitPerIp, _options.RateLimitWindow);
        var accountDecision = _rateLimiter.Acquire(
            $"otp:issue:account:{accountKey}", _options.IssuanceLimitPerAccount, _options.RateLimitWindow);

        if (!ipDecision.Allowed | !accountDecision.Allowed)
        {
            _events.IssuanceRateLimited(accountKey, clientAddress);
            var retryAfter = ipDecision.RetryAfter > accountDecision.RetryAfter
                ? ipDecision.RetryAfter
                : accountDecision.RetryAfter;
            return OtpIssuanceResult.RateLimited(retryAfter);
        }

        var accountExists = await _accountDirectory
            .AccountExistsAsync(emailAddress, cancellationToken)
            .ConfigureAwait(false);

        var code = OtpCodeGenerator.Generate(_options.CodeLength);
        var salt = OtpCodeHasher.NewSalt();
        var digest = OtpCodeHasher.ComputeDigest(code.RevealForDelivery(), salt);
        var now = _timeProvider.GetUtcNow();

        // Stored for unknown addresses too (AccountExists=false — can never
        // verify): issuance work and verification state stay identical for
        // both populations. Put replaces any prior record (AC-03).
        _store.Put(new OtpRecord(accountKey, salt, digest, now, now + _options.CodeLifetime, accountExists, 0));

        await _dispatcher
            .DispatchAsync(new OtpDispatchRequest(emailAddress, code, accountExists, now + _options.CodeLifetime), cancellationToken)
            .ConfigureAwait(false);

        _events.CodeIssued(accountKey);
        return OtpIssuanceResult.Accepted;
    }

    /// <summary>
    /// Verifies a submitted code. Success consumes the code (single-use,
    /// AC-02). All failures — wrong code, no outstanding code, expired,
    /// superseded, unknown account — return the same singleton
    /// <see cref="OtpVerificationResult.Failed"/> (AC-06). Repeated failures
    /// impose a lockout surfaced as <see cref="OtpVerificationOutcome.RateLimited"/>
    /// with a Retry-After (AC-04).
    /// </summary>
    public Task<OtpVerificationResult> VerifyCodeAsync(
        string emailAddress,
        string submittedCode,
        string clientAddress,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress);
        ArgumentNullException.ThrowIfNull(submittedCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientAddress);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(VerifyCore(emailAddress, submittedCode, clientAddress));
    }

    private OtpVerificationResult VerifyCore(string emailAddress, string submittedCode, string clientAddress)
    {
        var accountKey = OtpAccountKey.Derive(emailAddress);
        var now = _timeProvider.GetUtcNow();

        var ipDecision = _rateLimiter.Acquire(
            $"otp:verify:ip:{clientAddress}", _options.VerificationLimitPerIp, _options.RateLimitWindow);
        var accountDecision = _rateLimiter.Acquire(
            $"otp:verify:account:{accountKey}", _options.VerificationLimitPerAccount, _options.RateLimitWindow);

        if (!ipDecision.Allowed | !accountDecision.Allowed)
        {
            _events.VerificationRateLimited(accountKey, clientAddress);
            var retryAfter = ipDecision.RetryAfter > accountDecision.RetryAfter
                ? ipDecision.RetryAfter
                : accountDecision.RetryAfter;
            return OtpVerificationResult.RateLimited(retryAfter);
        }

        // Lockout accrues for unknown addresses exactly as for known ones
        // (decoy-population records fail and count too), so its presence
        // reveals attempt history, never account existence.
        if (_store.FindLockoutExpiry(accountKey) is { } lockedUntil && lockedUntil > now)
        {
            _events.VerificationRateLimited(accountKey, clientAddress);
            return OtpVerificationResult.RateLimited(lockedUntil - now);
        }

        var stored = _store.FindCurrent(accountKey);
        var recordFound = stored is not null;
        var record = stored ?? _decoy;

        // Constant-time core (AC-05): any-length submission is digested to a
        // fixed 32 bytes and compared with FixedTimeEquals; conditions are
        // combined with non-short-circuiting & so no factor exits early.
        var matches = OtpCodeHasher.Matches(submittedCode, record.Salt.Span, record.Digest.Span);
        var notExpired = record.ExpiresAt > now;
        var verified = matches & recordFound & record.AccountExists & notExpired;

        if (verified)
        {
            // Single-use: invalidated on use (AC-02).
            _store.Remove(accountKey);
            _events.VerificationSucceeded(accountKey);
            return OtpVerificationResult.Verified;
        }

        if (stored is not null)
        {
            var failures = stored.FailedAttempts + 1;
            if (failures >= _options.MaxVerificationFailures)
            {
                _store.Remove(accountKey);
                _store.SetLockoutExpiry(accountKey, now + _options.LockoutDuration);
                _events.LockoutImposed(accountKey);
            }
            else
            {
                _store.Put(stored.WithFailedAttempts(failures));
            }
        }

        _events.VerificationFailed(accountKey);
        return OtpVerificationResult.Failed;
    }
}
