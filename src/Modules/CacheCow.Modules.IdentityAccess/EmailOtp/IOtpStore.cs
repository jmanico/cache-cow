namespace CacheCow.Modules.IdentityAccess.EmailOtp;

/// <summary>
/// Port for server-side OTP state: hashed code records plus per-account
/// lockout marks, keyed by the pseudonymous account key (CC-SEC-016).
/// Implementations store only digests, never plaintext codes. The durable
/// implementation is a later persistence issue; the module registers
/// <see cref="InMemoryOtpStore"/> as a provisional default.
/// </summary>
public interface IOtpStore
{
    /// <summary>
    /// Stores <paramref name="record"/> as the single outstanding code for
    /// its account key, replacing any prior record — issuing a newer code
    /// invalidates every older one (CC-SEC-016; issue 059, AC-03).
    /// </summary>
    void Put(OtpRecord record);

    /// <summary>The single outstanding record for the account key, or null.</summary>
    OtpRecord? FindCurrent(string accountKey);

    /// <summary>Removes the outstanding record (single-use invalidation, AC-02).</summary>
    void Remove(string accountKey);

    /// <summary>When the account key's verification lockout ends, or null if none was set.</summary>
    DateTimeOffset? FindLockoutExpiry(string accountKey);

    /// <summary>Marks the account key locked out until <paramref name="expiresAt"/>.</summary>
    void SetLockoutExpiry(string accountKey, DateTimeOffset expiresAt);
}
