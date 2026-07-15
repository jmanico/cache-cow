namespace CacheCow.Host.Security;

/// <summary>
/// PII redaction helpers for log and telemetry fields (SECURITY.md, Logging
/// rules 4 and 8; CC-SEC-010, CC-CMP-003). The concrete platform redaction
/// policy (which fields per data class, masking vs. hashing vs. dropping) is
/// not defined in the specs and is owned by the retention/minimization issues
/// (issue 022, Open Questions); these helpers implement conservative masking
/// so no caller has an excuse to log a raw value in the meantime.
/// </summary>
public static class PiiRedactor
{
    private const string Redacted = "[redacted]";

    /// <summary>Masks the local part of an email entirely, keeping only the domain.</summary>
    public static string RedactEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Redacted;
        }

        var at = email.LastIndexOf('@');
        if (at <= 0 || at == email.Length - 1)
        {
            return Redacted;
        }

        return "***@" + LogSanitizer.Sanitize(email[(at + 1)..]);
    }

    /// <summary>
    /// Masks a value keeping at most its first character - enough to correlate
    /// during an investigation, not enough to reconstruct the value.
    /// </summary>
    public static string Mask(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Redacted;
        }

        var sanitized = LogSanitizer.Sanitize(value);
        return sanitized.Length <= 1 ? "***" : sanitized[0] + "***";
    }
}
