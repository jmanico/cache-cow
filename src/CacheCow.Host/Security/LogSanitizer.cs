using System.Text;

namespace CacheCow.Host.Security;

/// <summary>
/// Encodes user-supplied values before they enter log entries so hostile input
/// cannot forge additional entries or alter entry structure (SECURITY.md,
/// Logging rule 5; CC-SEC-010; issue 022 AC-04). CR/LF and every other control
/// character - including the ESC that introduces ANSI sequences - are replaced,
/// and values are truncated to a bounded length so a single field cannot flood
/// the log store.
/// </summary>
public static class LogSanitizer
{
    private const int MaxLength = 256;
    private const char Replacement = '_';

    public static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var truncated = value.Length > MaxLength ? value[..MaxLength] : value;
        var builder = new StringBuilder(truncated.Length);
        foreach (var c in truncated)
        {
            builder.Append(char.IsControl(c) ? Replacement : c);
        }

        if (value.Length > MaxLength)
        {
            builder.Append("...(truncated)");
        }

        return builder.ToString();
    }
}
