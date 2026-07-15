using CacheCow.Modules.WholesaleB2B.PriceLists;
using CacheCow.Modules.WholesaleB2B.Partners;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.WholesaleB2B.Tests;

/// <summary>
/// Issue 050, AC-01 (CC-WHS-001): per-partner per-market case-quantity price
/// lists in exactly the market's currency, integer minor units with
/// overflow-checked arithmetic that fails closed (CC-PRC-003) — including the
/// zero-decimal JPY and large INR amounts of CC-QA-004.
/// </summary>
public sealed class WholesalePriceListTests
{
    private static readonly PartnerId PartnerA = PartnerId.Parse("partner-a");
    private static readonly SkuId Brisket = SkuId.Parse("SKU-BRISKET");
    private static readonly SkuId Paneer = SkuId.Parse("SKU-PANEER");

    [Fact]
    [Requirement("CC-WHS-001")]
    public void A_price_list_carries_case_pack_and_per_case_price_per_sku()
    {
        var line = new WholesalePriceLine(Brisket, 12, Money.FromMinorUnits(35_988, Currency.Usd));
        var list = new WholesalePriceList(PartnerA, Market.US, [line]);

        Assert.Equal(PartnerA, list.Owner);
        Assert.Equal(Market.US, list.Market);
        Assert.True(list.TryGetLine(Brisket, out var found));
        Assert.Equal(12, found!.CasePackSize);
        Assert.Equal(Money.FromMinorUnits(35_988, Currency.Usd), found.PricePerCase);
        Assert.False(list.TryGetLine(Paneer, out _));
    }

    [Fact]
    [Requirement("CC-WHS-001")]
    public void A_line_priced_in_another_currency_than_the_markets_is_rejected()
    {
        // CC-PRC-001: DE wholesale prices are EUR; no FX conversion exists.
        var usdLine = new WholesalePriceLine(Brisket, 12, Money.FromMinorUnits(35_988, Currency.Usd));

        Assert.Throws<WholesaleValidationException>(
            () => new WholesalePriceList(PartnerA, Market.DE, [usdLine]));
    }

    [Fact]
    [Requirement("CC-WHS-001")]
    public void Duplicate_sku_rows_and_invalid_lines_are_rejected()
    {
        var line = new WholesalePriceLine(Brisket, 12, Money.FromMinorUnits(35_988, Currency.Usd));

        Assert.Throws<WholesaleValidationException>(
            () => new WholesalePriceList(PartnerA, Market.US, [line, line]));

        // Case pack size must be positive.
        Assert.Throws<WholesaleValidationException>(
            () => new WholesalePriceLine(Brisket, 0, Money.FromMinorUnits(35_988, Currency.Usd)));

        // Zero and negative case prices are rejected, never defaulted.
        Assert.Throws<WholesaleValidationException>(
            () => new WholesalePriceLine(Brisket, 12, Money.FromMinorUnits(0, Currency.Usd)));
        Assert.Throws<WholesaleValidationException>(
            () => new WholesalePriceLine(Brisket, 12, Money.FromMinorUnits(-1, Currency.Usd)));

        // An uninitialized owner or market never yields a list.
        Assert.Throws<WholesaleValidationException>(
            () => new WholesalePriceList(default, Market.US, [line]));
        Assert.Throws<WholesaleValidationException>(
            () => new WholesalePriceList(PartnerA, default, [line]));
    }

    [Fact]
    [Requirement("CC-WHS-001")]
    [Requirement("CC-PRC-003")]
    public void Case_quantity_extension_is_exact_integer_arithmetic_in_every_currency()
    {
        // JPY is zero-decimal: 180,000 yen per case x 7 cases.
        var jpLine = new WholesalePriceLine(Brisket, 24, Money.FromMinorUnits(180_000, Currency.Jpy));
        Assert.Equal(Money.FromMinorUnits(1_260_000, Currency.Jpy), jpLine.ExtendedPrice(7));

        // INR large amounts (lakh-scale): 1,24,900.00 rupees per case x 3.
        var inLine = new WholesalePriceLine(Paneer, 48, Money.FromMinorUnits(12_490_000, Currency.Inr));
        Assert.Equal(Money.FromMinorUnits(37_470_000, Currency.Inr), inLine.ExtendedPrice(3));

        // USD cents stay cents: $359.88 per case x 12.
        var usLine = new WholesalePriceLine(Brisket, 12, Money.FromMinorUnits(35_988, Currency.Usd));
        Assert.Equal(Money.FromMinorUnits(431_856, Currency.Usd), usLine.ExtendedPrice(12));

        // EUR for the DE/ES markets.
        var deLine = new WholesalePriceLine(Brisket, 12, Money.FromMinorUnits(29_988, Currency.Eur));
        Assert.Equal(Money.FromMinorUnits(59_976, Currency.Eur), deLine.ExtendedPrice(2));

        // MXN.
        var mxLine = new WholesalePriceLine(Brisket, 12, Money.FromMinorUnits(619_900, Currency.Mxn));
        Assert.Equal(Money.FromMinorUnits(1_239_800, Currency.Mxn), mxLine.ExtendedPrice(2));
    }

    [Fact]
    [Requirement("CC-WHS-001")]
    [Requirement("CC-PRC-003")]
    public void Case_quantity_overflow_fails_closed_and_bad_case_counts_are_rejected()
    {
        // Case counts are attacker-influenced quantities: overflow must throw,
        // never wrap into a small (or negative) total.
        var line = new WholesalePriceLine(Brisket, 12, Money.FromMinorUnits(long.MaxValue, Currency.Usd));
        Assert.Throws<MoneyOverflowException>(() => line.ExtendedPrice(2));

        var normal = new WholesalePriceLine(Brisket, 12, Money.FromMinorUnits(35_988, Currency.Usd));
        Assert.Throws<WholesaleValidationException>(() => normal.ExtendedPrice(0));
        Assert.Throws<WholesaleValidationException>(() => normal.ExtendedPrice(-3));
    }

    [Fact]
    [Requirement("CC-WHS-004")]
    public void Payment_terms_require_positive_net_days_and_default_to_net_60()
    {
        Assert.Equal(60, PaymentTerms.Net60Default.NetDays);
        Assert.Equal(PaymentTerms.Net(60), PaymentTerms.Net60Default);

        Assert.Throws<WholesaleValidationException>(() => PaymentTerms.Net(0));
        Assert.Throws<WholesaleValidationException>(() => PaymentTerms.Net(-30));

        // default(PaymentTerms) is uninitialized, never a silent net-0.
        Assert.Throws<WholesaleValidationException>(() => default(PaymentTerms).NetDays);
    }
}
