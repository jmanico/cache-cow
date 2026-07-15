namespace CacheCow.Modules.MarketGating.Caching;

/// <summary>
/// The caching decision for one response: either no-store, or cacheable under
/// a market+locale key. There is no way to express "cacheable without a key",
/// so an unkeyed (cross-market-reusable) cache entry is unrepresentable
/// (CC-MKT-009).
/// </summary>
public sealed record CacheDirective
{
    /// <summary>The Cache-Control value for uncacheable responses (SECURITY.md, HTTP boundary rule 3).</summary>
    public const string NoStoreHeaderValue = "no-store";

    private CacheDirective(GatedCacheKey? key)
    {
        Key = key;
    }

    /// <summary>The single no-store instance: never cached at any tier (SSR/output, edge, CDN).</summary>
    public static CacheDirective NoStore { get; } = new(key: null);

    public bool MayBeCached => Key is not null;

    /// <summary>The mandatory market+locale key when cacheable; null when no-store.</summary>
    public GatedCacheKey? Key { get; }

    public static CacheDirective Keyed(GatedCacheKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new CacheDirective(key);
    }
}
