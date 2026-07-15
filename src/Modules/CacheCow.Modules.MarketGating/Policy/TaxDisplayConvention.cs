namespace CacheCow.Modules.MarketGating.Policy;

/// <summary>
/// Per-market price/tax display convention (CC-PRC-002). The zero value is the
/// tax-inclusive convention, which is the majority (and consumer-safest)
/// default for the launch markets.
/// </summary>
public enum TaxDisplayConvention
{
    /// <summary>Prices displayed tax-inclusive (DE/ES/MX/JP/IN per CC-PRC-002).</summary>
    TaxInclusive = 0,

    /// <summary>Prices displayed tax-exclusive with estimated tax computed at checkout (US per CC-PRC-002).</summary>
    TaxExclusiveEstimatedAtCheckout = 1,
}
