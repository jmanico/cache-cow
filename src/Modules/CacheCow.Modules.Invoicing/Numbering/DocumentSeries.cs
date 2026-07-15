namespace CacheCow.Modules.Invoicing.Numbering;

/// <summary>
/// The document series a sequential number is allocated from. Invoices and
/// credit notes are numbered in separate per-legal-entity series so that
/// credit-note issuance can never create a gap in the invoice series
/// (CC-INV-001; issue 046 open question on strict gaplessness — the model
/// keeps each series independently gapless so either reading of "sequential"
/// is satisfiable).
/// </summary>
public enum DocumentSeries
{
    Invoice = 1,
    CreditNote = 2,
}
