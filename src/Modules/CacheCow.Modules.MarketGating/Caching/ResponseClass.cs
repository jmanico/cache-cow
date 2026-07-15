namespace CacheCow.Modules.MarketGating.Caching;

/// <summary>
/// Cacheability classes of a response (CC-MKT-009; SECURITY.md, HTTP boundary
/// rules 3 and 10). Only <see cref="AnonymousMarketGated"/> may ever be
/// cached; every other class — and any undeclared/unknown class, since the
/// zero value is personalized — is no-store, so a defaulted or invalid value
/// fails closed to uncacheable.
/// </summary>
public enum ResponseClass
{
    /// <summary>Per-user/personalized content — never cached (zero value: the fail-closed default).</summary>
    PerUserPersonalized = 0,

    /// <summary>Authenticated response — never cached.</summary>
    Authenticated = 1,

    /// <summary>Cart response — never cached.</summary>
    Cart = 2,

    /// <summary>Checkout response — never cached.</summary>
    Checkout = 3,

    /// <summary>
    /// Anonymous content already gated for one exact market/locale — cacheable,
    /// keyed strictly by <see cref="GatedCacheKey"/>. Includes the gated 404s
    /// of CC-MKT-004, which are keyed like any gated response (issue 028 AC-06).
    /// </summary>
    AnonymousMarketGated = 4,
}
