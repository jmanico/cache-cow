using System.Security.Cryptography;

namespace CacheCow.Modules.IdentityAccess.EmailOtp;

/// <summary>
/// Generates one-time codes from the cryptographic RNG (CC-SEC-016;
/// SECURITY.md, Authentication and authorization rule 13). Digits are drawn
/// by rejection sampling so the distribution is exactly uniform: a random
/// byte is accepted only when it is below 250 (the largest multiple of 10
/// that fits in a byte), which makes <c>value % 10</c> bias-free; bytes of
/// 250–255 are discarded and resampled. <c>System.Random</c> or modulo over
/// the full byte range would be defects (issue 059, Anti-Patterns).
/// </summary>
public static class OtpCodeGenerator
{
    // Largest multiple of 10 that fits in a byte. Accepting only values
    // below this bound makes `% 10` uniform over 0..9 (no modulo bias).
    private const int RejectionBound = 250;

    public static OtpCode Generate(int length)
    {
        if (length is < EmailOtpOptions.MinimumCodeLength or > EmailOtpOptions.MaximumCodeLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length),
                length,
                "OTP code length must be at least 6 digits (CC-SEC-016) and at most 32.");
        }

        Span<char> digits = stackalloc char[EmailOtpOptions.MaximumCodeLength];
        Span<byte> randomBytes = stackalloc byte[EmailOtpOptions.MaximumCodeLength];
        var produced = 0;

        while (produced < length)
        {
            RandomNumberGenerator.Fill(randomBytes);

            foreach (var value in randomBytes)
            {
                if (value >= RejectionBound)
                {
                    // Rejection sampling: discard, never fold back via modulo.
                    continue;
                }

                digits[produced++] = (char)('0' + (value % 10));

                if (produced == length)
                {
                    break;
                }
            }
        }

        return new OtpCode(new string(digits[..length]));
    }
}
