using CacheCow.Modules.Invoicing.Numbering;

namespace CacheCow.Modules.Invoicing.Invoices;

/// <summary>
/// The legally significant sequential number of an issued document: legal
/// entity plus a strictly sequential value from that entity's series
/// (CC-INV-001). Allocation happens only through
/// <see cref="ILegalEntitySequence"/>. This number is presentation for
/// documents and dashboards (IBM Plex Mono per DESIGN.md §4.1) and MUST NOT be
/// used in download links or other access paths — it is enumerable by design
/// (CC-INV-002; CC-ORD-010).
/// </summary>
public sealed class InvoiceNumber : IEquatable<InvoiceNumber>
{
    internal InvoiceNumber(LegalEntityId legalEntity, DocumentSeries series, long value)
    {
        if (value < 1)
        {
            throw new InvoiceValidationException(
                "Sequential document numbers start at 1 (CC-INV-001).");
        }

        LegalEntity = legalEntity;
        Series = series;
        Value = value;
    }

    public LegalEntityId LegalEntity { get; }

    public DocumentSeries Series { get; }

    /// <summary>Strictly sequential per legal entity and series, starting at 1.</summary>
    public long Value { get; }

    public bool Equals(InvoiceNumber? other) =>
        other is not null
        && LegalEntity.Equals(other.LegalEntity)
        && Series == other.Series
        && Value == other.Value;

    public override bool Equals(object? obj) => obj is InvoiceNumber other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(LegalEntity, Series, Value);

    /// <summary>
    /// Diagnostic form only. The legally rendered number format per market is
    /// part of the drafted invoice formats accepted 2026-07-15 and is applied
    /// at document rendering (issue 048), not here.
    /// </summary>
    public override string ToString() =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{LegalEntity.Value}:{Series}:{Value}");
}
