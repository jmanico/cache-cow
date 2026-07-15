using CacheCow.Modules.MarketGating.Resolution;

namespace CacheCow.Modules.MarketGating.Enforcement;

/// <summary>
/// Data that has passed gating for one exact transacting context. Constructible
/// only by the enforcement point (internal constructor), so a payload of this
/// type is proof-by-construction that it was gated — the only currency
/// <see cref="Caching.SsrTransferState"/> accepts (CC-MKT-009; SECURITY.md,
/// HTTP boundary rule 10).
/// </summary>
/// <typeparam name="T">The gated payload type.</typeparam>
public sealed class Gated<T>
{
    internal Gated(TransactingContext context, T value)
    {
        ArgumentNullException.ThrowIfNull(context);
        Context = context;
        Value = value;
    }

    /// <summary>The exact transacting context this payload was gated for.</summary>
    public TransactingContext Context { get; }

    public T Value { get; }
}
