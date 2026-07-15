namespace CacheCow.Modules.Fulfillment;

/// <summary>
/// A validated, normalized postal code as this context receives it. Per-market
/// address format validation (Japanese address structure, Indian PIN codes) is
/// owned by address capture (CC-ORD-002, issue 038); Fulfillment never operates
/// on raw client text (issue 045, AC-05). This type asserts only that the value
/// is present and trimmed — the market-specific shape was validated upstream.
/// </summary>
public readonly struct PostalCode : IEquatable<PostalCode>
{
    private readonly string? _value;

    private PostalCode(string value)
    {
        _value = value;
    }

    public string Value =>
        _value ?? throw new InvalidOperationException(
            "Uninitialized PostalCode value; use Parse or TryParse.");

    public static bool TryParse(string? value, out PostalCode postalCode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            postalCode = default;
            return false;
        }

        postalCode = new PostalCode(value.Trim());
        return true;
    }

    public static PostalCode Parse(string value) =>
        TryParse(value, out var postalCode)
            ? postalCode
            : throw new FormatException("A postal code must be a non-empty string (CC-ORD-002, CC-FUL-002).");

    public bool Equals(PostalCode other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is PostalCode other && Equals(other);

    public override int GetHashCode() => _value?.GetHashCode(StringComparison.Ordinal) ?? 0;

    public static bool operator ==(PostalCode left, PostalCode right) => left.Equals(right);

    public static bool operator !=(PostalCode left, PostalCode right) => !left.Equals(right);

    public override string ToString() => Value;
}
