namespace CacheCow.Modules.ContentLocalization.Resources.MessageFormat;

/// <summary>
/// A string resource failed ICU MessageFormat validation. Invalid resources
/// are rejected, never sanitized into acceptance (SECURITY.md, Input
/// validation rules 1 and 7; CC-I18N-002). The message names the rule and
/// position without echoing raw resource content (SECURITY.md, Logging rule 5).
/// </summary>
public sealed class IcuMessageFormatException : FormatException
{
    public IcuMessageFormatException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Message formatting failed at runtime: a required argument was missing or
/// of the wrong type. Formatting fails closed — a partially interpolated or
/// broken message is never produced (CC-I18N-006 posture; SECURITY.md,
/// Logging rule 2 applied to the rendering path).
/// </summary>
public sealed class IcuMessageArgumentException : ArgumentException
{
    public IcuMessageArgumentException(string message)
        : base(message)
    {
    }
}
