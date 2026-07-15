namespace CacheCow.Host.Security;

/// <summary>
/// Shared page-size clamp for list endpoints (SECURITY.md, HTTP boundary
/// rule 7; issue 018 AC-06): client-supplied page sizes are attacker
/// controlled and are clamped server-side to the configured maximum - no
/// endpoint hand-rolls its own paging bounds.
/// </summary>
public static class PageSizeLimiter
{
    public static int Clamp(int? requested, RequestLimitSettings limits)
    {
        ArgumentNullException.ThrowIfNull(limits);

        if (requested is null or <= 0)
        {
            return Math.Min(limits.DefaultPageSize, limits.MaxPageSize);
        }

        return Math.Min(requested.Value, limits.MaxPageSize);
    }
}
