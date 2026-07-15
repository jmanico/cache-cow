namespace CacheCow.Modules.BackOffice;

/// <summary>
/// Base for all fail-closed Back Office failures in this bounded context
/// (CC-DSH-001–006; SECURITY.md, Logging rule 2: any exception in an
/// authorization or gating path is a denial, never a bypass).
/// </summary>
public abstract class BackOfficeException : Exception
{
    protected BackOfficeException(string message)
        : base(message)
    {
    }

    protected BackOfficeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Invalid RBAC configuration: an unknown role or permission name in a
/// supplied role–permission matrix, or an out-of-bounds step-up policy value.
/// Rejected at load, never sanitized or defaulted into acceptance
/// (SECURITY.md, Input validation rule 1; Authentication rule 8's
/// documented, tested matrix).
/// </summary>
public sealed class RbacConfigurationException : BackOfficeException
{
    public RbacConfigurationException(string message)
        : base(message)
    {
    }

    public RbacConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// An audit event failed field validation (missing field, over-length value,
/// or control characters that would enable log injection). The event is
/// rejected and never stored partially or malformed (issue 081, Failure
/// Behavior; SECURITY.md, Input validation rule 1; Logging rule 5).
/// </summary>
public sealed class AuditEventValidationException : BackOfficeException
{
    public AuditEventValidationException(string message)
        : base(message)
    {
    }
}
