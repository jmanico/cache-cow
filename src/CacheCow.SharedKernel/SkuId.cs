namespace CacheCow.SharedKernel;

/// <summary>
/// Opaque SKU identity (CC-CAT-001: every SKU carries a unique ID). Value
/// equality; distinct from arbitrary strings at the type level. Classification
/// (veg/non-veg) and all other catalog data live in the Catalog context
/// (issue 029), never on the identity. The specs define no ID format beyond
/// uniqueness, so validation is limited to non-empty (issue 003, Open Questions).
/// </summary>
public readonly struct SkuId : IEquatable<SkuId>
{
    private readonly string? _value;

    private SkuId(string value)
    {
        _value = value;
    }

    public string Value =>
        _value ?? throw new InvalidOperationException(
            "Uninitialized SkuId value; use Parse or TryParse.");

    public static bool TryParse(string? value, out SkuId skuId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            skuId = default;
            return false;
        }

        skuId = new SkuId(value);
        return true;
    }

    public static SkuId Parse(string value) =>
        TryParse(value, out var skuId)
            ? skuId
            : throw new FormatException("A SKU ID must be a non-empty string (CC-CAT-001).");

    public bool Equals(SkuId other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is SkuId other && Equals(other);

    public override int GetHashCode() => _value?.GetHashCode(StringComparison.Ordinal) ?? 0;

    public static bool operator ==(SkuId left, SkuId right) => left.Equals(right);

    public static bool operator !=(SkuId left, SkuId right) => !left.Equals(right);

    public override string ToString() => Value;
}
