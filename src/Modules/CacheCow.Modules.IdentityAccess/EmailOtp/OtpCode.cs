namespace CacheCow.Modules.IdentityAccess.EmailOtp;

/// <summary>
/// A freshly generated one-time code. The value is a secret credential:
/// <see cref="ToString"/> is redacted so the code can never reach logs,
/// telemetry, or exception messages through incidental formatting
/// (SECURITY.md, Logging rule 4; CC-SEC-016). The only way to read the
/// digits is the explicit <see cref="RevealForDelivery"/> call, reserved
/// for the email dispatch adapter (<see cref="IOtpDispatcher"/>).
/// </summary>
public sealed class OtpCode
{
    private readonly string _digits;

    internal OtpCode(string digits)
    {
        ArgumentException.ThrowIfNullOrEmpty(digits);

        if (digits.Length < EmailOtpOptions.MinimumCodeLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(digits),
                digits.Length,
                "OTP codes must be at least 6 digits (CC-SEC-016).");
        }

        foreach (var c in digits)
        {
            if (!char.IsAsciiDigit(c))
            {
                // Deliberately does not echo the offending value (Logging rule 4).
                throw new ArgumentException("OTP codes must contain ASCII digits only.", nameof(digits));
            }
        }

        _digits = digits;
    }

    /// <summary>Number of digits in the code.</summary>
    public int Length => _digits.Length;

    /// <summary>
    /// Returns the secret digits for delivery to the user. Only the email
    /// dispatch path may call this; the value must never be logged,
    /// persisted, or placed in telemetry (only its keyed hash is stored).
    /// </summary>
    public string RevealForDelivery() => _digits;

    /// <summary>Redacted; never exposes the code (SECURITY.md, Logging rule 4).</summary>
    public override string ToString() => "OtpCode[REDACTED]";
}
