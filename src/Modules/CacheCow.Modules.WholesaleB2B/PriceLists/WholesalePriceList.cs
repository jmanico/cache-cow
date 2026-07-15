using CacheCow.Modules.WholesaleB2B.Partners;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.WholesaleB2B.PriceLists;

/// <summary>
/// A per-partner per-market wholesale price list (CC-WHS-001): owned by exactly
/// one partner tenant, denominated in exactly the market's launch currency
/// (CC-PRC-001), one row per SKU. This type is deliberately schema-disjoint
/// from every consumer catalog/price DTO — no consumer response model can carry
/// its fields even by accident (issue 050, Anti-Patterns; CC-WHS-003).
/// Retrieval always goes through <see cref="IWholesalePriceLists"/>, which
/// requires a <see cref="PartnerTenantContext"/>.
/// </summary>
public sealed class WholesalePriceList
{
    private readonly Dictionary<SkuId, WholesalePriceLine> _linesBySku;

    public WholesalePriceList(PartnerId owner, Market market, IEnumerable<WholesalePriceLine> lines)
    {
        if (owner == default)
        {
            throw new WholesaleValidationException(
                "A wholesale price list requires an owning partner tenant (CC-WHS-001, CC-WHS-003).");
        }

        ArgumentNullException.ThrowIfNull(lines);

        var currency = WholesaleMarketCurrencies.CurrencyOf(market);
        var bySku = new Dictionary<SkuId, WholesalePriceLine>();
        foreach (var line in lines)
        {
            ArgumentNullException.ThrowIfNull(line);

            if (!line.PricePerCase.Currency.Equals(currency))
            {
                throw new WholesaleValidationException(
                    $"A {market.Code} wholesale price must be denominated in {currency.Code}, not {line.PricePerCase.Currency.Code} (CC-WHS-001, CC-PRC-001; no runtime FX conversion exists).");
            }

            if (!bySku.TryAdd(line.Sku, line))
            {
                throw new WholesaleValidationException(
                    $"Duplicate wholesale price line for SKU '{line.Sku.Value}'; a price list carries exactly one row per SKU (CC-WHS-001).");
            }
        }

        Owner = owner;
        Market = market;
        _linesBySku = bySku;
    }

    /// <summary>The single tenant this list belongs to — the scoping key of every lookup (CC-WHS-003).</summary>
    public PartnerId Owner { get; }

    public Market Market { get; }

    public IReadOnlyCollection<WholesalePriceLine> Lines => _linesBySku.Values;

    public bool TryGetLine(SkuId sku, out WholesalePriceLine? line) =>
        _linesBySku.TryGetValue(sku, out line);
}
