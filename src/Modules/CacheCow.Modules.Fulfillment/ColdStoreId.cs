namespace CacheCow.Modules.Fulfillment;

/// <summary>
/// Typed identity of a regional cold store — "a fulfillment node holding frozen
/// inventory for one or more markets" (REQUIREMENTS.md §2). Owned by the
/// Fulfillment bounded context; the Catalog context has its own cold-store
/// concept and the host maps between contexts (ARCHITECTURE.md, Dependency
/// rule 9: everything beyond the shared kernel stays inside its context).
/// The actual cold-store topology is operational data that must be supplied,
/// not invented (issue 044, Open Questions).
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
            : throw new FormatException("A cold-store ID must be a non-empty string (CC-FUL-001).");

    public bool Equals(ColdStoreId other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is ColdStoreId other && Equals(other);

    public override int GetHashCode() => _value?.GetHashCode(StringComparison.Ordinal) ?? 0;

    public static bool operator ==(ColdStoreId left, ColdStoreId right) => left.Equals(right);

    public static bool operator !=(ColdStoreId left, ColdStoreId right) => !left.Equals(right);

    public override string ToString() => Value;
}
