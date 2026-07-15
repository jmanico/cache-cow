namespace CacheCow.Modules.PricingPromotions.Tests;

/// <summary>
/// Deterministic clock for promotion-window tests: evaluation takes the
/// authoritative server clock as an injected TimeProvider (CC-PRC-006), so
/// tests pin it to exact boundary instants.
/// </summary>
internal sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _utcNow;

    public FixedTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;
}
