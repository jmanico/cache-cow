using System.Security.Claims;
using CacheCow.Modules.WholesaleB2B.Auth;
using CacheCow.Modules.WholesaleB2B.Partners;

namespace CacheCow.Modules.WholesaleB2B.RateLimits;

/// <summary>
/// The named rate-limiter policies the B2B endpoints attach as metadata
/// (CC-API-008; SECURITY.md, HTTP boundary rule 7). The HOST owns the limiter
/// middleware (429 + Retry-After semantics, issue 019); this module owns the
/// contract: which policy guards which endpoint class, and how partitions are
/// keyed. The host must register both policies by these exact names,
/// partitioned per authenticated client via
/// <see cref="B2BRateLimitPartition"/>, with numeric budgets resolved through
/// <see cref="IB2BRateLimitTierSource"/>.
/// </summary>
public static class B2BRateLimitPolicies
{
    /// <summary>Every B2B endpoint: default 600 requests/minute per client (ratified 2026-07-15).</summary>
    public const string Client = "b2b-client";

    /// <summary>
    /// Order creation: 60/minute per client (ratified 2026-07-15). Matches the
    /// host's existing order-creation policy name (issue 019) and, being the
    /// endpoint-closest metadata, overrides <see cref="Client"/> on that route.
    /// </summary>
    public const string OrderCreation = "order-creation";
}

/// <summary>
/// Per-client partition-key provider the host plugs into its limiter
/// partitioner (CC-API-008: limits are per authenticated client, never shared,
/// never keyed on spoofable network attributes for authenticated traffic).
/// </summary>
public static class B2BRateLimitPartition
{
    public const string KeyPrefix = "b2b-client:";

    /// <summary>
    /// The partition key for an authenticated B2B principal, or null when no
    /// client identity is present — unauthenticated requests are rejected 401
    /// by authentication (which runs before the limiter, SECURITY.md HTTP
    /// boundary rule 5) and MUST NOT consume any partner's quota; the host
    /// falls back to its anonymous keying for them (issue 019).
    /// </summary>
    public static string? KeyFor(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.Identity is not { IsAuthenticated: true })
        {
            return null;
        }

        var clientId = B2BTokenClaims.ClientId(principal);
        return clientId is null ? null : KeyPrefix + clientId;
    }
}

/// <summary>
/// One partner tier's budgets (CC-API-008). Values are configuration data,
/// not code; the ratified defaults are 600 requests/minute overall and
/// 60/minute for order creation.
/// </summary>
public sealed record B2BRateLimitTier
{
    public B2BRateLimitTier(int requestsPerMinute, int orderCreationsPerMinute)
    {
        if (requestsPerMinute <= 0 || orderCreationsPerMinute <= 0)
        {
            throw new WholesaleValidationException(
                "Rate-limit tiers require positive per-minute budgets (CC-API-008).");
        }

        RequestsPerMinute = requestsPerMinute;
        OrderCreationsPerMinute = orderCreationsPerMinute;
    }

    /// <summary>The ratified 2026-07-15 defaults: 600/min overall, 60/min order creation.</summary>
    public static B2BRateLimitTier RatifiedDefault { get; } = new(600, 60);

    public int RequestsPerMinute { get; }

    public int OrderCreationsPerMinute { get; }
}

/// <summary>
/// Configuration port resolving a partner's tier budgets (CC-API-008 "tune per
/// partner tier"). Named partner tiers are an open decision (issue 056, Open
/// Questions); until a human defines them, the only guaranteed answer is the
/// ratified default, and overrides are per-partner data.
/// </summary>
public interface IB2BRateLimitTierSource
{
    B2BRateLimitTier TierFor(PartnerId partnerId);
}

/// <summary>
/// In-memory tier configuration: ratified defaults with per-partner overrides
/// (an operator assigning a tier changes one partner's budgets without code
/// changes and without affecting any other partner — issue 056, AC-03).
/// </summary>
public sealed class InMemoryB2BRateLimitTierSource : IB2BRateLimitTierSource
{
    private readonly Lock _gate = new();
    private readonly Dictionary<PartnerId, B2BRateLimitTier> _overrides = [];

    public void SetTier(PartnerId partnerId, B2BRateLimitTier tier)
    {
        ArgumentNullException.ThrowIfNull(tier);
        if (partnerId == default)
        {
            throw new WholesaleValidationException("A tier override requires a partner identity (CC-API-008).");
        }

        lock (_gate)
        {
            _overrides[partnerId] = tier;
        }
    }

    public B2BRateLimitTier TierFor(PartnerId partnerId)
    {
        lock (_gate)
        {
            return _overrides.TryGetValue(partnerId, out var tier)
                ? tier
                : B2BRateLimitTier.RatifiedDefault;
        }
    }
}
