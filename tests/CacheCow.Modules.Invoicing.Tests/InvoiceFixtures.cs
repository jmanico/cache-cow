using CacheCow.Modules.Invoicing.Invoices;
using CacheCow.Modules.Invoicing.Numbering;
using CacheCow.Modules.Invoicing.Tax;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.Invoicing.Tests;

/// <summary>
/// Canonical test fixtures. Legal-entity IDs are test inputs only: the legal
/// entities per market are an open decision (issue 046, Open Questions) and
/// the domain model requires them as configuration/input with no defaults.
/// All money is integer minor units — no binary floating point, including in
/// tests (CC-PRC-003).
/// </summary>
internal static class InvoiceFixtures
{
    public static readonly LegalEntityId TestEntity = LegalEntityId.Parse("LE-TEST-1");
    public static readonly LegalEntityId OtherTestEntity = LegalEntityId.Parse("LE-TEST-2");

    public static readonly DateTimeOffset IssuedAt = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    public static InvoiceIssuer NewIssuer() => new(new InMemoryLegalEntitySequence());

    public static InvoiceLine Line(Currency currency, long unitPriceMinorUnits, long quantity = 1, string description = "Smoked brisket, 1.2 kg, frozen") =>
        new(description, SkuId.Parse("SKU-TEST-1"), quantity, Money.FromMinorUnits(unitPriceMinorUnits, currency));

    public static MarketTaxContent TaxContentFor(Market market, long taxMinorUnits = 100, int lineCount = 1)
    {
        if (market == Market.US)
        {
            return new UsSalesTaxContent(
                [new UsSalesTaxLine("CA state + district", 0.0875m, Money.FromMinorUnits(taxMinorUnits, Currency.Usd))]);
        }

        if (market == Market.ES || market == Market.DE)
        {
            return new EuVatTaxContent(
                market,
                "DE811111111",
                [new EuVatLine(0.19m, Money.FromMinorUnits(taxMinorUnits, Currency.Eur))]);
        }

        if (market == Market.JP)
        {
            return new JpConsumptionTaxContent(
                "T1234567890123",
                [new JpConsumptionTaxLine(0.10m, Money.FromMinorUnits(taxMinorUnits, Currency.Jpy))]);
        }

        if (market == Market.IN)
        {
            var details = Enumerable.Range(1, lineCount)
                .Select(lineNumber => new IndiaGstLineDetail(
                    lineNumber, "0402", 0.05m, Money.FromMinorUnits(taxMinorUnits, Currency.Inr)))
                .ToArray();
            return new IndiaGstTaxContent("29ZZZZZ9999Z1Z5", details);
        }

        throw new InvalidOperationException($"No ratified fixture tax content for market '{market.Code}'.");
    }

    public static InvoiceDraft Draft(
        Market market,
        long unitPriceMinorUnits = 14900,
        long quantity = 1,
        AccountReference? customerAccount = null,
        LegalEntityId? legalEntity = null)
    {
        var currency = LaunchMarketCurrencies.For(market);
        return new InvoiceDraft(
            legalEntity ?? TestEntity,
            market,
            OrderReference.Parse("order-test-0001"),
            customerAccount,
            [Line(currency, unitPriceMinorUnits, quantity)],
            TaxContentFor(market),
            IssuedAt);
    }
}
