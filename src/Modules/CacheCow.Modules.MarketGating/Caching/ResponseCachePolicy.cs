using CacheCow.Modules.MarketGating.Resolution;

namespace CacheCow.Modules.MarketGating.Caching;

/// <summary>
/// In-process cache-safety policy (CC-MKT-009, CC-SEC-013; SECURITY.md, HTTP
/// boundary rule 10): personalized/authenticated/cart/checkout responses are
/// no-store; anonymous gated responses are cacheable only under a key derived
/// from the server-side transacting context; an unresolved gating context is
/// no-store (fail closed — never cached under a guessed or default key).
/// These are the primitives the SSR/output-cache and edge tiers consume; the
/// concrete edge/CDN product is an open decision (ARCHITECTURE.md, "Known
/// unknowns") and its configuration is out of scope here.
/// </summary>
public static class ResponseCachePolicy
{
    /// <summary>
    /// Classifies a response for caching. Only
    /// <see cref="ResponseClass.AnonymousMarketGated"/> with a resolved
    /// transacting context is cacheable; everything else — including undefined
    /// <paramref name="responseClass"/> values and a null
    /// <paramref name="gatingContext"/> — is no-store (issue 028 AC-03/AC-05).
    /// </summary>
    public static CacheDirective Classify(ResponseClass responseClass, TransactingContext? gatingContext)
    {
        if (responseClass != ResponseClass.AnonymousMarketGated)
        {
            return CacheDirective.NoStore;
        }

        return gatingContext is null
            ? CacheDirective.NoStore // fail closed: never cache under a guessed key (AC-05)
            : CacheDirective.Keyed(GatedCacheKey.FromContext(gatingContext));
    }
}
