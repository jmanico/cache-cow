using CacheCow.Modules.MarketGating.Enforcement;
using CacheCow.Modules.MarketGating.Resolution;

namespace CacheCow.Modules.MarketGating.Caching;

/// <summary>
/// The container the SSR layer serializes into HTML as Angular
/// transfer/hydration state. It accepts only <see cref="Gated{T}"/> payloads —
/// producible solely by the enforcement point — and only when they were gated
/// for exactly this response's transacting context. An ungated catalog,
/// another market's SKUs (e.g. non-veg in IN), or data gated for a different
/// context is unaddable by construction (CC-MKT-009, CC-SEC-013; SECURITY.md,
/// HTTP boundary rule 10; ARCHITECTURE.md, "Edge &amp; SSR caching").
/// </summary>
public sealed class SsrTransferState
{
    private readonly Dictionary<string, object?> _entries = new(StringComparer.Ordinal);

    private SsrTransferState(TransactingContext context)
    {
        Context = context;
    }

    /// <summary>The exact transacting context this response is gated for.</summary>
    public TransactingContext Context { get; }

    public static SsrTransferState For(TransactingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SsrTransferState(context);
    }

    /// <summary>
    /// Adds a gated payload under a hydration key. Rejects (fail closed) any
    /// payload gated for a different market/locale than this response — the
    /// exception surfaces as a denial, never a leak (SECURITY.md, Logging rule 2).
    /// </summary>
    public void Set<T>(string key, Gated<T> payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(payload);

        if (payload.Context != Context)
        {
            throw new InvalidOperationException(
                $"Transfer state for {Context.Market.Code}/{Context.Locale.Tag} rejected a payload gated for "
                + $"{payload.Context.Market.Code}/{payload.Context.Locale.Tag}: hydration state may carry only data "
                + "gated for this exact response (CC-MKT-009; SECURITY.md, HTTP boundary rule 10).");
        }

        _entries[key] = payload.Value;
    }

    /// <summary>The gated entries, ready for serialization by the SSR layer.</summary>
    public IReadOnlyDictionary<string, object?> Entries => _entries;
}
