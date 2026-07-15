namespace CacheCow.Modules.CatalogInventory.Catalog;

/// <summary>
/// Cut/category of a SKU (CC-CAT-001), e.g. brisket, ribs, paneer. Modeled as
/// a validated opaque identifier, not a closed enum: the cut/category taxonomy
/// is not defined anywhere in the specs (issue 029, Open Questions — it also
/// feeds the Cuts diagram and per-category promotion scope) and inventing one
/// here would resolve an open decision (CLAUDE.md working rules). Non-empty,
/// no surrounding whitespace; invalid values are rejected, never trimmed into
/// acceptance (SECURITY.md, Input validation rule 1).
/// </summary>
public readonly struct CutCategory : IEquatable<CutCategory>
{
    private readonly string? _value;

    private CutCategory(string value)
    {
        _value = value;
    }

    public string Value =>
        _value ?? throw new InvalidOperationException(
            "Uninitialized CutCategory value; use Parse or TryParse.");

    public static bool TryParse(string? value, out CutCategory cutCategory)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length != value.Trim().Length)
        {
            cutCategory = default;
            return false;
        }

        cutCategory = new CutCategory(value);
        return true;
    }

    public static CutCategory Parse(string value) =>
        TryParse(value, out var cutCategory)
            ? cutCategory
            : throw new FormatException(
                "A cut/category must be a non-empty identifier without surrounding whitespace (CC-CAT-001).");

    public bool Equals(CutCategory other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is CutCategory other && Equals(other);

    public override int GetHashCode() => _value?.GetHashCode(StringComparison.Ordinal) ?? 0;

    public static bool operator ==(CutCategory left, CutCategory right) => left.Equals(right);

    public static bool operator !=(CutCategory left, CutCategory right) => !left.Equals(right);

    public override string ToString() => Value;
}
