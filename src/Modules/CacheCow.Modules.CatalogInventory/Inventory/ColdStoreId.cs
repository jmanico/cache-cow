namespace CacheCow.Modules.CatalogInventory.Inventory;

/// <summary>
/// Identity of a regional cold store — a fulfillment node holding frozen
/// inventory for one or more markets (REQUIREMENTS.md §2). Owned by the
/// Catalog &amp; Inventory context (issue 030); fulfillment routing has its own
/// view of stores behind its own boundary. Value equality; non-empty.
/// </summary>
public readonly struct ColdStoreId : IEquatable<ColdStoreId>
{
    private readonly string? _value;

    private ColdStoreId(string value)
    {
        _value = value;
    }

    public string Value =>
        _value ?? throw new InvalidOperationException(
            "Uninitialized ColdStoreId value; use Parse or TryParse.");

    public static bool TryParse(string? value, out ColdStoreId coldStoreId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            coldStoreId = default;
            return false;
        }

        coldStoreId = new ColdStoreId(value);
        return true;
    }

    public static ColdStoreId Parse(string value) =>
        TryParse(value, out var coldStoreId)
            ? coldStoreId
            : throw new FormatException("A cold-store ID must be a non-empty string (CC-CAT-002).");

    public bool Equals(ColdStoreId other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is ColdStoreId other && Equals(other);

    public override int GetHashCode() => _value?.GetHashCode(StringComparison.Ordinal) ?? 0;

    public static bool operator ==(ColdStoreId left, ColdStoreId right) => left.Equals(right);

    public static bool operator !=(ColdStoreId left, ColdStoreId right) => !left.Equals(right);

    public override string ToString() => Value;
}
