namespace CacheCow.Modules.IdentityAccess.EmailOtp;

/// <summary>
/// The stored server-side state for one outstanding OTP: only the keyed,
/// salted digest of the code — never the plaintext (CC-SEC-016). Records are
/// stored under the pseudonymous account key for known AND unknown addresses
/// (unknown addresses get a real record with <see cref="AccountExists"/>
/// false), so issuance and verification take the same path either way and
/// account existence is never observable (issue 059, AC-06).
/// </summary>
public sealed class OtpRecord
{
    public OtpRecord(
        string accountKey,
        ReadOnlyMemory<byte> salt,
        ReadOnlyMemory<byte> digest,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        bool accountExists,
        int failedAttempts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountKey);

        if (salt.Length != OtpCodeHasher.SaltSizeBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(salt), salt.Length, "Salt must be 32 bytes.");
        }

        if (digest.Length != OtpCodeHasher.DigestSizeBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(digest), digest.Length, "Digest must be 32 bytes.");
        }

        if (expiresAt <= issuedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAt), expiresAt, "Expiry must be after issuance.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(failedAttempts);

        AccountKey = accountKey;
        Salt = salt;
        Digest = digest;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        AccountExists = accountExists;
        FailedAttempts = failedAttempts;
    }

    /// <summary>Pseudonymous account key (<see cref="OtpAccountKey"/>), never a raw email address.</summary>
    public string AccountKey { get; }

    /// <summary>Per-code random HMAC key.</summary>
    public ReadOnlyMemory<byte> Salt { get; }

    /// <summary>HMAC-SHA256 digest of the code; the plaintext is never stored.</summary>
    public ReadOnlyMemory<byte> Digest { get; }

    public DateTimeOffset IssuedAt { get; }

    /// <summary>At most 10 minutes after <see cref="IssuedAt"/> (CC-SEC-016).</summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// Whether the address mapped to an account at issuance. Records for
    /// unknown addresses can never verify, but exist so both populations
    /// share one code path (enumeration safety).
    /// </summary>
    public bool AccountExists { get; }

    /// <summary>Failed verification attempts against this code.</summary>
    public int FailedAttempts { get; }

    public OtpRecord WithFailedAttempts(int failedAttempts) =>
        new(AccountKey, Salt, Digest, IssuedAt, ExpiresAt, AccountExists, failedAttempts);

    /// <summary>Redacted; record contents stay out of logs and exception text (Logging rule 4).</summary>
    public override string ToString() => "OtpRecord[REDACTED]";
}
