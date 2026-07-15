using System.Reflection;
using CacheCow.Modules.Invoicing.Invoices;
using CacheCow.Modules.Invoicing.Tax;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.Invoicing.Tests;

/// <summary>
/// Issue 046: once issued, an invoice has NO mutation path (AC-06) —
/// structurally verified by reflection — and corrections happen only via
/// credit notes that reference and never touch the original (AC-03).
/// Database-privilege enforcement (INSERT-only roles, WORM — CC-SEC-020)
/// lands with the persistence issues; these tests pin the application layer's
/// half: the aggregate itself offers nothing a mutation could call.
/// </summary>
public sealed class InvoiceImmutabilityTests
{
    private static readonly Type[] IssuedRecordTypes =
    [
        typeof(Invoice),
        typeof(InvoiceLine),
        typeof(InvoiceNumber),
        typeof(CreditNote),
        typeof(MarketTaxContent),
        typeof(UsSalesTaxContent),
        typeof(UsSalesTaxLine),
        typeof(EuVatTaxContent),
        typeof(EuVatLine),
        typeof(JpConsumptionTaxContent),
        typeof(JpConsumptionTaxLine),
        typeof(IndiaGstTaxContent),
        typeof(IndiaGstLineDetail),
    ];

    public static TheoryData<Type> IssuedRecordTypeData()
    {
        var data = new TheoryData<Type>();
        foreach (var type in IssuedRecordTypes)
        {
            data.Add(type);
        }

        return data;
    }

    [Theory]
    [Requirement("CC-INV-001")]
    [Requirement("CC-SEC-020")]
    [MemberData(nameof(IssuedRecordTypeData))]
    public void Issued_record_types_expose_no_setter_and_no_mutable_field(Type type)
    {
        var settableProperties = type
            .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(property => property.SetMethod is not null)
            .Select(property => $"{type.Name}.{property.Name}")
            .ToArray();

        Assert.True(
            settableProperties.Length == 0,
            "Settable properties on issued financial records (CC-INV-001): " + string.Join(", ", settableProperties));

        var mutableFields = type
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(field => !field.IsInitOnly)
            .Select(field => $"{type.Name}.{field.Name}")
            .ToArray();

        Assert.True(
            mutableFields.Length == 0,
            "Mutable fields on issued financial records (CC-INV-001): " + string.Join(", ", mutableFields));
    }

    [Theory]
    [Requirement("CC-INV-001")]
    [MemberData(nameof(IssuedRecordTypeData))]
    public void No_public_method_offers_an_edit_operation(Type type)
    {
        // AC-06: no code path exposes an invoice "edit" — every public
        // instance method is a pure reader (non-void, no mutator naming).
        var mutators = type
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Where(method => method.ReturnType == typeof(void)
                || method.Name.StartsWith("Set", StringComparison.Ordinal)
                || method.Name.StartsWith("Update", StringComparison.Ordinal)
                || method.Name.StartsWith("Edit", StringComparison.Ordinal)
                || method.Name.StartsWith("Add", StringComparison.Ordinal)
                || method.Name.StartsWith("Remove", StringComparison.Ordinal)
                || method.Name.StartsWith("Delete", StringComparison.Ordinal)
                || method.Name.StartsWith("Apply", StringComparison.Ordinal))
            .Select(method => $"{type.Name}.{method.Name}")
            .ToArray();

        Assert.True(
            mutators.Length == 0,
            "Mutation-shaped public methods on issued financial records (issue 046, AC-06): " + string.Join(", ", mutators));
    }

    [Fact]
    [Requirement("CC-INV-001")]
    public void Invoice_lines_are_a_defensive_read_only_copy()
    {
        var issuer = InvoiceFixtures.NewIssuer();
        var lines = new List<InvoiceLine> { InvoiceFixtures.Line(Currency.Usd, 14900) };
        var draft = new InvoiceDraft(
            InvoiceFixtures.TestEntity,
            Market.US,
            OrderReference.Parse("order-test-0003"),
            customerAccount: null,
            lines,
            InvoiceFixtures.TaxContentFor(Market.US),
            InvoiceFixtures.IssuedAt);

        var invoice = issuer.Issue(draft);

        // Mutating the caller's list after issuance cannot reach the record.
        lines.Add(InvoiceFixtures.Line(Currency.Usd, 999));
        Assert.Single(invoice.Lines);

        // And the exposed collection itself rejects mutation.
        if (invoice.Lines is IList<InvoiceLine> asList)
        {
            Assert.True(asList.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => asList.Add(InvoiceFixtures.Line(Currency.Usd, 1)));
            Assert.Throws<NotSupportedException>(() => asList.RemoveAt(0));
        }
    }

    [Fact]
    [Requirement("CC-INV-001")]
    [Requirement("CC-SEC-020")]
    public void Credit_note_references_the_original_and_leaves_it_unchanged()
    {
        var issuer = InvoiceFixtures.NewIssuer();
        var original = issuer.Issue(InvoiceFixtures.Draft(Market.DE, unitPriceMinorUnits: 14900));

        var snapshot = Snapshot(original);

        var creditNote = issuer.IssueCreditNote(
            original,
            "Partial refund: one damaged pack",
            Money.FromMinorUnits(5000, Currency.Eur),
            InvoiceFixtures.IssuedAt.AddDays(3));

        // The correction is a NEW record referencing the original…
        Assert.Equal(original.Id, creditNote.OriginalInvoiceId);
        Assert.Equal(original.Number, creditNote.OriginalInvoiceNumber);
        Assert.NotEqual(original.Number, creditNote.Number);

        // …and the original is observably unchanged (AC-03).
        Assert.Equal(snapshot, Snapshot(original));
    }

    [Fact]
    [Requirement("CC-INV-001")]
    public void Credit_note_cannot_exceed_the_original_total_or_change_currency()
    {
        var issuer = InvoiceFixtures.NewIssuer();
        var original = issuer.Issue(InvoiceFixtures.Draft(Market.JP, unitPriceMinorUnits: 14900));

        Assert.Throws<InvoiceValidationException>(() => issuer.IssueCreditNote(
            original, "over-credit", Money.FromMinorUnits(15000, Currency.Jpy), InvoiceFixtures.IssuedAt));
        Assert.Throws<InvoiceValidationException>(() => issuer.IssueCreditNote(
            original, "wrong currency", Money.FromMinorUnits(100, Currency.Usd), InvoiceFixtures.IssuedAt));
        Assert.Throws<InvoiceValidationException>(() => issuer.IssueCreditNote(
            original, "non-positive", Money.FromMinorUnits(0, Currency.Jpy), InvoiceFixtures.IssuedAt));
    }

    [Fact]
    [Requirement("CC-PRC-007")]
    [Requirement("CC-INV-001")]
    public void Presentation_promotion_naming_is_rejected_in_legal_line_descriptions()
    {
        // "Eviction Specials" is storefront clearance vocabulary (DESIGN.md
        // 5.3) and never a legal description (CC-PRC-007; issue 046, AC-05).
        Assert.Throws<InvoiceValidationException>(() => new InvoiceLine(
            "EVICTION SPECIAL: brisket ends",
            SkuId.Parse("SKU-TEST-1"),
            1,
            Money.FromMinorUnits(1000, Currency.Usd)));
    }

    private static string Snapshot(Invoice invoice) =>
        string.Join(
            '|',
            invoice.Id.Value,
            invoice.Number.ToString(),
            invoice.LegalEntity.Value,
            invoice.Market.Code,
            invoice.Order.Value,
            invoice.IssuedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            invoice.Subtotal.ToString(),
            invoice.Total.ToString(),
            invoice.TaxContent.TaxTotal.ToString(),
            string.Join(';', invoice.Lines.Select(line =>
                $"{line.LegalDescription}/{line.Quantity}/{line.UnitPrice}/{line.LineTotal}")));
}
