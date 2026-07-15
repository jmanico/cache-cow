using System.Security.Cryptography;
using System.Text;

namespace CacheCow.Modules.IdentityAccess.EmailOtp;

/// <summary>
/// Keyed, salted hashing for stored OTP codes (CC-SEC-016). Only the
/// HMAC-SHA256 digest of a code — keyed with a per-code random salt — is ever
/// persisted; the plaintext code exists only in flight to the email dispatch
/// port. Verification is constant-time by construction: every submitted
/// value, whatever its length or content, is first mapped to a fixed 32-byte
/// digest and then compared with
/// <see cref="CryptographicOperations.FixedTimeEquals"/>, so there is no
/// early-exit, length-dependent, or content-dependent comparison path
/// (issue 059, AC-05).
/// </summary>
public static class OtpCodeHasher
{
    /// <summary>Per-code random HMAC key size.</summary>
    public const int SaltSizeBytes = 32;

    /// <summary>HMAC-SHA256 output size; every digest has exactly this length.</summary>
    public const int DigestSizeBytes = 32;

    /// <summary>Creates a fresh per-code salt from the cryptographic RNG.</summary>
    public static byte[] NewSalt() => RandomNumberGenerator.GetBytes(SaltSizeBytes);

    /// <summary>
    /// Maps any submitted value to a fixed-length keyed digest. Inputs of any
    /// length — including empty and wrong-length submissions — take this same
    /// path, which is what makes the subsequent fixed-time comparison
    /// length-independent.
    /// </summary>
    public static byte[] ComputeDigest(string code, ReadOnlySpan<byte> salt)
    {
        ArgumentNullException.ThrowIfNull(code);

        if (salt.Length != SaltSizeBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(salt), salt.Length, "Salt must be 32 bytes.");
        }

        return HMACSHA256.HashData(salt, Encoding.UTF8.GetBytes(code));
    }

    /// <summary>
    /// Constant-time comparison of a submitted value against a stored digest:
    /// digest-then-<see cref="CryptographicOperations.FixedTimeEquals"/> over
    /// two 32-byte arrays (CC-SEC-016; issue 059, AC-05).
    /// </summary>
    public static bool Matches(string submittedCode, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> storedDigest)
    {
        var candidate = ComputeDigest(submittedCode, salt);
        return CryptographicOperations.FixedTimeEquals(candidate, storedDigest);
    }
}
