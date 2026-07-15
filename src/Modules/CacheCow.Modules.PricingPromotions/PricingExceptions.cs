using CacheCow.SharedKernel;

namespace CacheCow.Modules.PricingPromotions;

/// <summary>Base for all fail-closed pricing/promotion failures in this bounded context (CC-PRC-001/003/006; SECURITY.md, Logging rule 2).</summary>
public abstract class PricingException : Exception
{
    protected PricingException(string message)
        : base(message)
    {
    }

    protected PricingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Invalid pricing/promotion input or configuration: rejected outright, never
/// sanitized or defaulted into acceptance (SECURITY.md, Input validation rule 1).
/// Also raised when a computation would require an unratified policy decision
/// (e.g. an unspecified rounding mode — issue 033, Open Questions).
/// </summary>
public class PricingValidationException : PricingException
{
    public PricingValidationException(string message)
        : base(message)
    {
    }

    public PricingValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// A SKU has no price in the transacting market. There is no default, no
/// fallback to another market's price, and no FX-derived value: the SKU is
/// simply not purchasable there (CC-PRC-001; issue 032 AC-03, fail closed).
/// </summary>
public sealed class PriceUnavailableException : PricingException
{
    public PriceUnavailableException(SkuId sku, Market market)
        : base($"SKU '{sku.Value}' has no price in market {market.Code}; it is not purchasable there and no FX-derived or cross-market fallback exists (CC-PRC-001).")
    {
    }
}

/// <summary>
/// No IANA timezone is configured for a market whose promotion windows must be
/// evaluated. The market-to-timezone mapping is an unratified decision
/// (issue 033, Open Questions); evaluation fails closed rather than guessing a
/// zone — a failure never grants or extends a discount (SECURITY.md, Logging rule 2).
/// </summary>
public sealed class MarketTimeZoneUnavailableException : PricingException
{
    public MarketTimeZoneUnavailableException(Market market)
        : base($"No timezone is configured for market {market.Code}; promotion windows cannot be evaluated (CC-PRC-006). The market-to-IANA-timezone mapping is an open decision (issue 033, Open Questions) and must be supplied as configuration.")
    {
    }
}
