namespace CacheCow.Modules.Invoicing.Invoices;

/// <summary>
/// Opaque reference to the originating order in the Ordering &amp; Payments
/// bounded context. Cross-context needs are ports and opaque identifiers —
/// the Invoicing module never references another module (ARCHITECTURE.md,
/// Dependency rule 9); order semantics stay in Ordering (issues 035/036).
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
            "Uninitialized OrderReference; use Parse or TryParse.");

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
            : throw new FormatException("An order reference must be a non-empty string.");

    public bool Equals(OrderReference other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is OrderReference other && Equals(other);

    public override int GetHashCode() => _value?.GetHashCode(StringComparison.Ordinal) ?? 0;

    public static bool operator ==(OrderReference left, OrderReference right) => left.Equals(right);

    public static bool operator !=(OrderReference left, OrderReference right) => !left.Equals(right);

    public override string ToString() => Value;
}
