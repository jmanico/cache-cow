using CacheCow.Modules.Invoicing.Invoices;
using CacheCow.Modules.Invoicing.Tax;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.Invoicing.Tests;

/// <summary>
/// Issue 047: every invoice carries exactly its market's legally required tax
/// content shape — right shape accepted, wrong shape rejected at construction,
/// MX fails closed pending ratification (open question 8).
/// </summary>
public sealed class MarketTaxContentTests
{
    private static readonly string[] Ratified = ["US", "ES", "DE", "JP", "IN"];

    public static TheoryData<string> RatifiedMarketCodes() => new(Ratified);

    [Theory]
    [Requirement("CC-INV-001")]
    [MemberData(nameof(RatifiedMarketCodes))]
    public void Right_shape_is_accepted_for_every_ratified_market(string marketCode)
    {
        var market = Market.Parse(marketCode);
        var issuer = InvoiceFixtures.NewIssuer();

        var invoice = issuer.Issue(InvoiceFixtures.Draft(market));

        Assert.Equal(market, invoice.Market);
        Assert.Equal(market, invoice.TaxContent.Market);
        Assert.Equal(LaunchMarketCurrencies.For(market), invoice.TaxContent.TaxTotal.Currency);
    }

    [Theory]
    [Requirement("CC-INV-001")]
    [MemberData(nameof(RatifiedMarketCodes))]
    public void Wrong_shape_is_rejected_at_construction_for_every_market_pair(string marketCode)
    {
        var market = Market.Parse(marketCode);
        var issuer = InvoiceFixtures.NewIssuer();
        var currency = LaunchMarketCurrencies.For(market);

        foreach (var otherCode in Ratified.Where(code => code != marketCode))
        {
            var other = Market.Parse(otherCode);
            if (LaunchMarketCurrencies.For(other) == currency)
            {
                continue; // ES/DE share EUR and the EU VAT shape carries its own market tag — covered below.
            }

            var draft = new InvoiceDraft(
                InvoiceFixtures.TestEntity,
                market,
                OrderReference.Parse("order-test-0004"),
                customerAccount: null,
                [InvoiceFixtures.Line(currency, 1000)],
                InvoiceFixtures.TaxContentFor(other),
                InvoiceFixtures.IssuedAt);

            Assert.Throws<InvoiceValidationException>(() => issuer.Issue(draft));
        }
    }

    [Fact]
    [Requirement("CC-INV-001")]
    public void Es_invoice_rejects_content_tagged_for_de_even_though_both_are_eur()
    {
        var issuer = InvoiceFixtures.NewIssuer();
        var draft = new InvoiceDraft(
            InvoiceFixtures.TestEntity,
            Market.ES,
            OrderReference.Parse("order-test-0005"),
            customerAccount: null,
            [InvoiceFixtures.Line(Currency.Eur, 1000)],
            InvoiceFixtures.TaxContentFor(Market.DE),
            InvoiceFixtures.IssuedAt);

        Assert.Throws<InvoiceValidationException>(() => issuer.Issue(draft));
    }

    [Fact]
    [Requirement("CC-INV-001")]
    public void Us_content_carries_sales_tax_lines_and_is_tax_exclusive()
    {
        var content = Assert.IsType<UsSalesTaxContent>(InvoiceFixtures.TaxContentFor(Market.US));

        Assert.False(content.AmountsAreTaxInclusive); // CC-PRC-002
        Assert.NotEmpty(content.TaxLines);
        Assert.All(content.TaxLines, line => Assert.Equal(Currency.Usd, line.Amount.Currency));

        Assert.Throws<InvoiceValidationException>(() => new UsSalesTaxContent([]));
    }

    [Fact]
    [Requirement("CC-INV-001")]
    public void Eu_content_requires_rates_and_vat_identifier_and_only_eu_markets()
    {
        var content = Assert.IsType<EuVatTaxContent>(InvoiceFixtures.TaxContentFor(Market.DE));

        Assert.True(content.AmountsAreTaxInclusive); // CC-PRC-002
        Assert.False(string.IsNullOrWhiteSpace(content.SellerVatIdentifier)); // USt-IdNr.
        Assert.All(content.VatLines, line => Assert.InRange(line.Rate, 0m, 1m));

        Assert.Throws<InvoiceValidationException>(() => new EuVatTaxContent(
            Market.JP, "DE811111111", [new EuVatLine(0.19m, Money.FromMinorUnits(1, Currency.Eur))]));
        Assert.Throws<InvoiceValidationException>(() => new EuVatTaxContent(
            Market.DE, "  ", [new EuVatLine(0.19m, Money.FromMinorUnits(1, Currency.Eur))]));
        Assert.Throws<InvoiceValidationException>(() => new EuVatTaxContent(Market.DE, "DE811111111", []));
    }

    [Fact]
    [Requirement("CC-INV-001")]
    public void Jp_content_requires_the_qualified_invoice_number_and_jpy_amounts()
    {
        var content = Assert.IsType<JpConsumptionTaxContent>(InvoiceFixtures.TaxContentFor(Market.JP));

        Assert.True(content.AmountsAreTaxInclusive);
        Assert.False(string.IsNullOrWhiteSpace(content.QualifiedInvoiceNumber));
        Assert.Equal(Currency.Jpy, content.TaxTotal.Currency);

        Assert.Throws<InvoiceValidationException>(() => new JpConsumptionTaxContent(
            "", [new JpConsumptionTaxLine(0.10m, Money.FromMinorUnits(1, Currency.Jpy))]));

        // JPY is zero-decimal: sub-yen amounts are unrepresentable (CC-PRC-003).
        Assert.Throws<InvalidMoneyException>(() => Money.FromDecimal(0.5m, Currency.Jpy));
    }

    [Fact]
    [Requirement("CC-INV-001")]
    public void In_content_requires_gstin_and_an_hsn_code_per_line()
    {
        var content = Assert.IsType<IndiaGstTaxContent>(InvoiceFixtures.TaxContentFor(Market.IN, lineCount: 1));

        Assert.True(content.AmountsAreTaxInclusive); // inclusive with explicit GST line (CC-PRC-002)
        Assert.False(string.IsNullOrWhiteSpace(content.SellerGstin));
        Assert.All(content.LineDetails, detail => Assert.False(string.IsNullOrWhiteSpace(detail.HsnCode)));

        Assert.Throws<InvoiceValidationException>(() => new IndiaGstTaxContent("", content.LineDetails));
        Assert.Throws<InvoiceValidationException>(() => new IndiaGstLineDetail(
            1, "", 0.05m, Money.FromMinorUnits(1, Currency.Inr)));
    }

    [Fact]
    [Requirement("CC-INV-001")]
    public void In_invoice_rejects_gst_detail_that_does_not_cover_every_line_exactly()
    {
        var issuer = InvoiceFixtures.NewIssuer();

        // Two lines, GST detail for only one: per-line HSN coverage is mandatory.
        var draft = new InvoiceDraft(
            InvoiceFixtures.TestEntity,
            Market.IN,
            OrderReference.Parse("order-test-0006"),
            customerAccount: null,
            [InvoiceFixtures.Line(Currency.Inr, 1000), InvoiceFixtures.Line(Currency.Inr, 2000)],
            InvoiceFixtures.TaxContentFor(Market.IN, lineCount: 1),
            InvoiceFixtures.IssuedAt);

        Assert.Throws<InvoiceValidationException>(() => issuer.Issue(draft));
    }

    [Fact]
    [Requirement("CC-INV-001")]
    public void Mx_invoice_issuance_fails_closed_pending_ratification()
    {
        // Epic open question 8: CC-INV-001 enumerates no MX tax content. The
        // placeholder is explicit and unconstructible; issuance fails closed.
        Assert.Throws<UnratifiedMarketTaxContentException>(() => MexicoTaxContent.Compose());

        var issuer = InvoiceFixtures.NewIssuer();
        var draft = new InvoiceDraft(
            InvoiceFixtures.TestEntity,
            Market.MX,
            OrderReference.Parse("order-test-0007"),
            customerAccount: null,
            [InvoiceFixtures.Line(Currency.Mxn, 14900)],
            InvoiceFixtures.TaxContentFor(Market.US), // any shape: MX is rejected before shape checking
            InvoiceFixtures.IssuedAt);

        Assert.Throws<UnratifiedMarketTaxContentException>(() => issuer.Issue(draft));
    }

    [Fact]
    [Requirement("CC-INV-001")]
    public void No_tax_content_shape_exists_outside_the_module()
    {
        // The hierarchy is closed: every constructor of the abstract base is
        // private protected, so "market X invoice with market Y shape" cannot
        // be smuggled in via an external subtype (issue 047).
        var constructors = typeof(MarketTaxContent)
            .GetConstructors(System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic);

        Assert.All(constructors, ctor => Assert.False(
            ctor.IsPublic || ctor.IsFamily || ctor.IsFamilyOrAssembly,
            "MarketTaxContent must not be subclassable outside the Invoicing module (issue 047)."));
    }
}
