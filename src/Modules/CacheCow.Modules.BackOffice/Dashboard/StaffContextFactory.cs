using System.Globalization;
using System.Security.Claims;
using CacheCow.Modules.BackOffice.Rbac;

namespace CacheCow.Modules.BackOffice.Dashboard;

/// <summary>
/// The claim names the dashboard endpoints read a <see cref="StaffContext"/>
/// from. Staff authentication itself is HOST scope — SSO against Microsoft
/// Entra ID with mandatory passkeys (CC-DSH-001; SECURITY.md, Authentication
/// rule 2; ARCHITECTURE.md, "Authentication model") — so this module only
/// consumes the principal the host already authenticated, and never validates
/// a token or trusts a client-supplied header (SECURITY.md, Input validation
/// rule 3).
/// </summary>
public static class DashboardClaimTypes
{
    /// <summary>The authenticated staff identity (OIDC <c>sub</c>).</summary>
    public const string Subject = "sub";

    /// <summary>The staff RBAC role (Entra ID emits app roles as <c>roles</c>); resolved against the closed CC-DSH-002 set.</summary>
    public const string Role = "roles";

    /// <summary>
    /// OIDC <c>auth_time</c>: seconds since the Unix epoch at which the end
    /// user last authenticated. A step-up ceremony re-runs authentication and
    /// therefore advances it, which is exactly the recency signal
    /// <see cref="StepUpPolicy"/> measures (SECURITY.md, Authentication
    /// rule 2).
    ///
    /// FLAGGED: that <c>auth_time</c> is the step-up signal is this module's
    /// reading of the standard claim, not a ratified platform decision — the
    /// canonical documents name no claim. The host's step-up implementation
    /// (issue 060) owns the real contract and may substitute another claim
    /// name here; this module reads whatever the configured factory supplies.
    /// </summary>
    public const string AuthenticationTime = "auth_time";
}

/// <summary>
/// Mints the server-side <see cref="StaffContext"/> for a request from the
/// host-authenticated principal. Returns null whenever a trustworthy staff
/// identity cannot be established — the endpoints then answer 401 and no
/// permission check ever runs against a half-known actor (fail closed,
/// SECURITY.md, Logging rule 2).
/// </summary>
public interface IStaffContextFactory
{
    /// <summary>The staff context for this principal, or null if none can be established.</summary>
    StaffContext? Create(ClaimsPrincipal? principal);
}

/// <summary>
/// Reads <see cref="StaffContext"/> out of the authenticated principal's
/// claims (<see cref="DashboardClaimTypes"/>).
///
/// Fail-closed rules, all of them deliberate:
/// <list type="bullet">
/// <item>an unauthenticated principal yields null;</item>
/// <item>a missing or blank subject yields null — an unattributable actor
/// cannot be audited, and CC-DSH-004 requires the actor on every event;</item>
/// <item>MULTIPLE role claims yield null rather than a guess. Whether roles
/// are combinable per staff member is an open question (issue 080, Open
/// Questions); picking the first, or the most privileged, would silently
/// resolve it. Denying until a human decides is the only safe reading
/// (CLAUDE.md, working rules);</item>
/// <item>an unparseable <c>auth_time</c> yields a context with NO recorded
/// re-authentication rather than a context with a fabricated one — sensitive
/// permissions then deny (SECURITY.md, Authentication rule 2). A malformed
/// claim must never read as "recently re-authenticated".</item>
/// </list>
/// The role string is passed through raw: resolution against the closed role
/// set is <see cref="DashboardAuthorizationService"/>'s, so an unknown role is
/// a denial rather than a crash in the request path.
/// </summary>
public sealed class ClaimsStaffContextFactory : IStaffContextFactory
{
    public StaffContext? Create(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var subject = Single(principal, DashboardClaimTypes.Subject)
            ?? Single(principal, ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        var role = Single(principal, DashboardClaimTypes.Role);
        if (role is null)
        {
            return null;
        }

        return StaffContext.ForAuthenticatedStaff(subject, role, ReadAuthenticationTime(principal));
    }

    /// <summary>The claim's value if it appears EXACTLY once; null if absent or ambiguous.</summary>
    private static string? Single(ClaimsPrincipal principal, string claimType)
    {
        string? found = null;
        foreach (var claim in principal.FindAll(claimType))
        {
            if (found is not null)
            {
                // Ambiguous: two values, no rule for choosing between them.
                return null;
            }

            found = claim.Value;
        }

        return found;
    }

    private static DateTimeOffset? ReadAuthenticationTime(ClaimsPrincipal principal)
    {
        var value = Single(principal, DashboardClaimTypes.AuthenticationTime);
        if (value is null)
        {
            return null;
        }

        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var unixSeconds))
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            // Out-of-range value: treat as no re-authentication at all.
            return null;
        }
    }
}
