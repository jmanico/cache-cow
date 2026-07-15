using System.Security.Cryptography;
using System.Text;

namespace CacheCow.Modules.IdentityAccess.EmailOtp;

/// <summary>
/// Derives the pseudonymous per-account key used for OTP storage, rate-limit
/// scoping, lockout tracking, and security-event logging. The key is a
/// SHA-256 hash of the normalized email address, computed identically whether
/// or not the address maps to an account — so every code path is keyed the
/// same way for known and unknown addresses (enumeration safety, CC-SEC-016)
/// and no raw email address needs to enter rate-limiter state or log entries
/// (SECURITY.md, Logging rule 4: redact PII).
/// </summary>
public static class OtpAccountKey
{
    public static string Derive(string emailAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress);

        // Uppercase normalization per CA1308 guidance; ordinal semantics.
        var normalized = emailAddress.Trim().ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }
}
