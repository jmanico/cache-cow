namespace CacheCow.Modules.Fulfillment;

/// <summary>
/// Opaque reference to an order owned by the Ordering &amp; Payments context.
/// Fulfillment never reaches into that context's data; the host maps its order
/// identity into this typed reference at the seam (ARCHITECTURE.md,
/// "Packaging": contexts may split into services later along the same seams).
/// </summary>
public readonly struct OrderReference : IEquatable<OrderReference>
{
    private readonly string? _value;

    private OrderReference(string value)
    {
        _value = value;
    }

    public string Value =>
        _value ?? throw new InvalidOperationException(
            "Uninitialized OrderReference value; use Parse or TryParse.");

    public static bool TryParse(string? value, out OrderReference orderReference)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            orderReference = default;
            return false;
        }

        orderReference = new OrderReference(value);
        return true;
    }

    public static OrderReference Parse(string value) =>
        TryParse(value, out var orderReference)
            ? orderReference
            : throw new FormatException("An order reference must be a non-empty string (CC-FUL-001).");

    public bool Equals(OrderReference other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is OrderReference other && Equals(other);

    public override int GetHashCode() => _value?.GetHashCode(StringComparison.Ordinal) ?? 0;

    public static bool operator ==(OrderReference left, OrderReference right) => left.Equals(right);

    public static bool operator !=(OrderReference left, OrderReference right) => !left.Equals(right);

    public override string ToString() => Value;
}
