namespace CacheCow.Modules.Invoicing.Numbering;

/// <summary>
/// Port: per-legal-entity, per-series sequential document numbering
/// (CC-INV-001: "sequential numbering per legal entity").
///
/// Contract — strictly gapless and sequential per (legal entity, series):
/// <list type="bullet">
/// <item>The first allocation for an entity/series returns 1.</item>
/// <item>Every subsequent allocation returns exactly the previous value + 1 —
/// no gaps, no duplicates, no reuse — including under concurrent
/// allocation.</item>
/// <item>Sequences for different legal entities (and different series of the
/// same entity) are fully independent; there is never one global sequence
/// across entities (issue 046, Implementation Notes).</item>
/// <item>Allocation MUST be atomic with issuance of the document that consumes
/// the number: callers allocate only after all validation has passed, so a
/// failed issuance never discards a number (issue 046, Failure Behavior —
/// no number allocated without a persisted invoice record). Whether market
/// law requires strict gaplessness versus unique-and-monotonic is an open
/// question (issue 046); this contract satisfies the stricter reading.</item>
/// </list>
/// The production adapter (PostgreSQL, per-entity transactional allocation) is
/// blocked on the data-residency/write-region open decision (ARCHITECTURE.md,
/// "Known unknowns"); <see cref="InMemoryLegalEntitySequence"/> serves tests.
/// </summary>
public interface ILegalEntitySequence
{
    /// <summary>Allocates the next number in the entity's series, starting at 1.</summary>
    long AllocateNext(LegalEntityId legalEntity, DocumentSeries series);
}
