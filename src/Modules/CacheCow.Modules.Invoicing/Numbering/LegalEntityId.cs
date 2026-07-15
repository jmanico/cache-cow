namespace CacheCow.Modules.Invoicing.Numbering;

/// <summary>
/// Identity of the legal entity whose invoice-number sequence a document
/// belongs to (CC-INV-001: "sequential numbering per legal entity").
///
/// The legal entities per market are NOT enumerated anywhere in the canonical
/// specs (issue 046, Open Questions; epic open question 9). This type therefore
/// carries no static members, no defaults, and no per-market mapping: the legal
/// entity is required configuration/input supplied by a human decision, never
/// invented here (CLAUDE.md working rules).
/// </summary>
public readonly struct LegalEntityId : IEquatable<LegalEntityId>
{
    private readonly string? _value;

    private LegalEntityId(string value)
    {
        _value = value;
    }

    public string Value =>
        _value ?? throw new InvalidOperationException(
            "Uninitialized LegalEntityId; use Parse or TryParse (issue 046 — legal entities are required configuration).");

    public static bool TryParse(string? value, out LegalEntityId legalEntityId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            legalEntityId = default;
            return false;
        }

        legalEntityId = new LegalEntityId(value);
        return true;
    }

    public static LegalEntityId Parse(string value) =>
        TryParse(value, out var legalEntityId)
            ? legalEntityId
            : throw new FormatException("A legal entity ID must be a non-empty string (CC-INV-001).");

    public bool Equals(LegalEntityId other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is LegalEntityId other && Equals(other);

    public override int GetHashCode() => _value?.GetHashCode(StringComparison.Ordinal) ?? 0;

    public static bool operator ==(LegalEntityId left, LegalEntityId right) => left.Equals(right);

    public static bool operator !=(LegalEntityId left, LegalEntityId right) => !left.Equals(right);

    public override string ToString() => Value;
}
