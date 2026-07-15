namespace CacheCow.Host.Security;

/// <summary>
/// First-party claim types carried by CacheCow principals. Caller identity and
/// tenancy are read exclusively from these server-side validated claims - never
/// from route, query, or body values (SECURITY.md, Input validation rule 3;
/// Authentication rules 9-10). The concrete claim mapping from the Entra
/// providers lands with issues 054/058/060.
/// </summary>
public static class CacheCowClaims
{
    /// <summary>
    /// Server-issued session identifier (CC-SEC-006). Stamped at sign-in by
    /// <see cref="ISessionLifecycle"/>, checked against
    /// <see cref="ISessionRevocation"/> on every request by
    /// <see cref="RevocationValidatingCookieEvents"/>. A cookie principal
    /// without this claim is rejected (fail closed).
    /// </summary>
    public const string SessionId = "cachecow:sid";

    /// <summary>
    /// The caller's tenant (B2B partner or wholesale-buyer partner scope).
    /// Object-level authorization matches resource ownership against this
    /// claim (CC-SEC-007, CC-API-004, CC-WHS-003).
    /// </summary>
    public const string TenantId = "cachecow:tenant";
}
