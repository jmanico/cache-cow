using CacheCow.Modules.Invoicing.Invoices;
using CacheCow.Modules.Invoicing.Tax;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.Invoicing.Tests;

/// <summary>
/// Money exactness on the invoice path in all five launch currencies
/// (CC-PRC-003; CC-QA-004): integer minor units end to end, JPY zero-decimal,
/// INR large-value grouping magnitudes, overflow fails closed — and no binary
/// floating point anywhere in these tests.
/// </summary>
public sealed class MoneyExactnessTests
{
    public static TheoryData<string, long, long, long> LineArithmeticCases()
    {
        // currency, unit price (minor units), quantity, expected line total.
        return new TheoryData<string, long, long, long>
        {
            { "USD", 14900, 3, 44700 },
            { "EUR", 14900, 3, 44700 },
            { "MXN", 14900, 3, 44700 }, // line-level arithmetic is currency-complete even while MX issuance awaits ratification
            { "JPY", 14900, 3, 44700 }, // zero-decimal: minor unit IS the yen
            { "INR", 124900000, 100, 12490000000 }, // lakh/crore-scale magnitudes stay exact (grouping is rendering, CC-PRC-004)
        };
    }

    [Theory]
    [Requirement("CC-PRC-003")]
    [Requirement("CC-INV-001")]
    [MemberData(nameof(LineArithmeticCases))]
    public void Line_totals_are_exact_integer_minor_units_in_every_currency(
        string currencyCode, long unitPrice, long quantity, long expectedTotal)
    {
        var currency = Currency.Parse(currencyCode);

        var line = InvoiceFixtures.Line(currency, unitPrice, quantity);

        Assert.Equal(expectedTotal, line.LineTotal.MinorUnits);
        Assert.Equal(currency, line.LineTotal.Currency);
    }

    [Fact]
    [Requirement("CC-PRC-003")]
    [Requirement("CC-INV-001")]
    public void Attacker_scale_quantities_overflow_closed_never_wrapping()
    {
        // Quantities are attacker-influenced (CC-PRC-003): the multiplication
        // aborts instead of wrapping into a small (or negative) legal total.
        Assert.Throws<MoneyOverflowException>(() =>
            InvoiceFixtures.Line(Currency.Inr, long.MaxValue / 2, quantity: 3));
    }

    [Fact]
    [Requirement("CC-PRC-003")]
    [Requirement("CC-INV-001")]
    public void Us_total_adds_exclusive_tax_exactly()
    {
        var issuer = InvoiceFixtures.NewIssuer();
        var draft = new InvoiceDraft(
            InvoiceFixtures.TestEntity,
            Market.US,
            OrderReference.Parse("order-test-0008"),
            customerAccount: null,
            [InvoiceFixtures.Line(Currency.Usd, 14900, quantity: 2)],
            new UsSalesTaxContent([new UsSalesTaxLine("CA", 0.0875m, Money.FromMinorUnits(2608, Currency.Usd))]),
            InvoiceFixtures.IssuedAt);

        var invoice = issuer.Issue(draft);

        Assert.Equal(29800, invoice.Subtotal.MinorUnits);
        Assert.Equal(2608, invoice.TaxContent.TaxTotal.MinorUnits);
        Assert.Equal(32408, invoice.Total.MinorUnits); // tax-exclusive market: total = subtotal + tax (CC-PRC-002)
    }

    [Theory]
    [Requirement("CC-PRC-003")]
    [Requirement("CC-INV-001")]
    [InlineData("DE", 14900L)]
    [InlineData("ES", 14900L)]
    [InlineData("JP", 14900L)]
    [InlineData("IN", 1249000000L)]
    public void Tax_inclusive_totals_equal_the_subtotal_exactly(string marketCode, long unitPrice)
    {
        var market = Market.Parse(marketCode);
        var issuer = InvoiceFixtures.NewIssuer();

        var invoice = issuer.Issue(InvoiceFixtures.Draft(market, unitPriceMinorUnits: unitPrice));

        Assert.Equal(unitPrice, invoice.Subtotal.MinorUnits);
        Assert.Equal(invoice.Subtotal, invoice.Total); // tax already inside prices (CC-PRC-002)
        Assert.True(invoice.TaxContent.AmountsAreTaxInclusive);
    }

    [Fact]
    [Requirement("CC-PRC-001")]
    [Requirement("CC-INV-001")]
    public void Cross_currency_lines_are_rejected_no_runtime_fx_exists()
    {
        var issuer = InvoiceFixtures.NewIssuer();
        var draft = new InvoiceDraft(
            InvoiceFixtures.TestEntity,
            Market.JP,
            OrderReference.Parse("order-test-0009"),
            customerAccount: null,
            [InvoiceFixtures.Line(Currency.Usd, 14900)], // USD line on a JP invoice
            InvoiceFixtures.TaxContentFor(Market.JP),
            InvoiceFixtures.IssuedAt);

        Assert.Throws<InvoiceValidationException>(() => issuer.Issue(draft));
    }

    [Fact]
    [Requirement("CC-PRC-003")]
    public void Tax_totals_sum_exactly_with_overflow_checked_addition()
    {
        Assert.Throws<MoneyOverflowException>(() => new UsSalesTaxContent(
        [
            new UsSalesTaxLine("A", 0.05m, Money.FromMinorUnits(long.MaxValue, Currency.Usd)),
            new UsSalesTaxLine("B", 0.05m, Money.FromMinorUnits(1, Currency.Usd)),
        ]));
    }
}
