using System.Collections.Concurrent;
using CacheCow.Modules.Invoicing.Invoices;
using CacheCow.Modules.Invoicing.Numbering;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.Invoicing.Tests;

/// <summary>
/// Issue 046: sequential numbering per legal entity — strictly gapless,
/// starting at 1, independent across entities and series, and race-free under
/// concurrent issuance (AC-01).
/// </summary>
public sealed class LegalEntitySequenceTests
{
    [Fact]
    [Requirement("CC-INV-001")]
    public void Sequence_is_gapless_and_starts_at_one_per_entity()
    {
        var sequence = new InMemoryLegalEntitySequence();

        var allocated = Enumerable.Range(0, 100)
            .Select(_ => sequence.AllocateNext(InvoiceFixtures.TestEntity, DocumentSeries.Invoice))
            .ToArray();

        Assert.Equal(Enumerable.Range(1, 100).Select(i => (long)i), allocated);
    }

    [Fact]
    [Requirement("CC-INV-001")]
    public void Sequences_are_independent_across_legal_entities()
    {
        var sequence = new InMemoryLegalEntitySequence();

        Assert.Equal(1, sequence.AllocateNext(InvoiceFixtures.TestEntity, DocumentSeries.Invoice));
        Assert.Equal(2, sequence.AllocateNext(InvoiceFixtures.TestEntity, DocumentSeries.Invoice));

        // A second entity starts its own series at 1 — never one global
        // sequence across entities (issue 046, Implementation Notes).
        Assert.Equal(1, sequence.AllocateNext(InvoiceFixtures.OtherTestEntity, DocumentSeries.Invoice));
        Assert.Equal(3, sequence.AllocateNext(InvoiceFixtures.TestEntity, DocumentSeries.Invoice));
        Assert.Equal(2, sequence.AllocateNext(InvoiceFixtures.OtherTestEntity, DocumentSeries.Invoice));
    }

    [Fact]
    [Requirement("CC-INV-001")]
    public void Credit_note_series_never_gaps_the_invoice_series()
    {
        var sequence = new InMemoryLegalEntitySequence();

        Assert.Equal(1, sequence.AllocateNext(InvoiceFixtures.TestEntity, DocumentSeries.Invoice));
        Assert.Equal(1, sequence.AllocateNext(InvoiceFixtures.TestEntity, DocumentSeries.CreditNote));
        Assert.Equal(2, sequence.AllocateNext(InvoiceFixtures.TestEntity, DocumentSeries.Invoice));
    }

    [Fact]
    [Requirement("CC-INV-001")]
    public void Uninitialized_legal_entity_cannot_key_a_sequence()
    {
        var sequence = new InMemoryLegalEntitySequence();

        // Legal entities are required configuration with no default (issue 046,
        // Open Questions); a default-valued ID fails closed.
        Assert.ThrowsAny<InvalidOperationException>(
            () => sequence.AllocateNext(default, DocumentSeries.Invoice));
    }

    [Fact]
    [Requirement("CC-INV-001")]
    [Requirement("CC-SEC-020")]
    public async Task Concurrent_issuance_produces_gapless_unique_numbers_per_entity()
    {
        var issuer = InvoiceFixtures.NewIssuer();
        const int perEntity = 200;

        var usNumbers = new ConcurrentBag<long>();
        var deNumbers = new ConcurrentBag<long>();

        // Interleave two entities across many concurrent issuances (AC-01:
        // no duplicates under concurrent issuance; cross-entity independence).
        var tasks = Enumerable.Range(0, perEntity).SelectMany(_ => new[]
        {
            Task.Run(() => usNumbers.Add(
                issuer.Issue(InvoiceFixtures.Draft(Market.US, legalEntity: InvoiceFixtures.TestEntity)).Number.Value)),
            Task.Run(() => deNumbers.Add(
                issuer.Issue(InvoiceFixtures.Draft(Market.DE, legalEntity: InvoiceFixtures.OtherTestEntity)).Number.Value)),
        });

        await Task.WhenAll(tasks);

        // Exactly 1..N per entity: unique, monotonic, gapless.
        Assert.Equal(Enumerable.Range(1, perEntity).Select(i => (long)i), usNumbers.Order());
        Assert.Equal(Enumerable.Range(1, perEntity).Select(i => (long)i), deNumbers.Order());
    }

    [Fact]
    [Requirement("CC-INV-001")]
    public void Rejected_draft_never_consumes_a_sequence_number()
    {
        var sequence = new InMemoryLegalEntitySequence();
        var issuer = new InvoiceIssuer(sequence);

        // Wrong-shape tax content (issue 047): validation fails before any
        // allocation, so gaplessness survives the failure (046 Failure Behavior).
        var wrongShape = new InvoiceDraft(
            InvoiceFixtures.TestEntity,
            Market.US,
            OrderReference.Parse("order-test-0002"),
            customerAccount: null,
            [InvoiceFixtures.Line(Currency.Usd, 1000)],
            InvoiceFixtures.TaxContentFor(Market.JP),
            InvoiceFixtures.IssuedAt);

        Assert.Throws<InvoiceValidationException>(() => issuer.Issue(wrongShape));

        var next = issuer.Issue(InvoiceFixtures.Draft(Market.US));
        Assert.Equal(1, next.Number.Value);
    }
}
