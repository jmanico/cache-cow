using CacheCow.Modules.MarketGating.Resolution;

namespace CacheCow.Modules.MarketGating.Caching;

/// <summary>
/// A cache key derived exclusively from the server-side transacting market +
/// locale (CC-MKT-009, CC-SEC-013; SECURITY.md, HTTP boundary rule 10).
/// Enforced by construction: the only way to obtain a key is
/// <see cref="FromContext"/>, whose sole input is <see cref="TransactingContext"/> —
/// Accept-Language, geolocation, and client-forgeable cookies cannot
/// contribute because no API accepts them. Two requests that differ only in
/// client hints therefore always produce identical keys.
/// </summary>
public sealed record GatedCacheKey
{
    private GatedCacheKey(string value)
    {
        Value = value;
    }

    /// <summary>Canonical key text, e.g. "market=IN|locale=hi-IN".</summary>
    public string Value { get; }

    public static GatedCacheKey FromContext(TransactingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new GatedCacheKey($"market={context.Market.Code}|locale={context.Locale.Tag}");
    }

    public override string ToString() => Value;
}
