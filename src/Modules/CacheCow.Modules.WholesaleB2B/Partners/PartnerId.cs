namespace CacheCow.Modules.WholesaleB2B.Partners;

/// <summary>
/// Opaque partner tenant identity (CC-WHS-002): the scoping key behind which
/// all wholesale prices, terms, orders, and invoices live (CC-WHS-003,
/// CC-API-004; ARCHITECTURE.md, Dependency rule 3). Value equality; distinct
/// from arbitrary strings at the type level. No ID format is specified beyond
/// uniqueness, so validation is limited to non-empty.
/// </summary>
public readonly struct PartnerId : IEquatable<PartnerId>
{
    private readonly string? _value;

    private PartnerId(string value)
    {
        _value = value;
    }

    public string Value =>
        _value ?? throw new InvalidOperationException(
            "Uninitialized PartnerId value; use Parse or TryParse.");

    public static bool TryParse(string? value, out PartnerId partnerId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            partnerId = default;
            return false;
        }

        partnerId = new PartnerId(value);
        return true;
    }

    public static PartnerId Parse(string value) =>
        TryParse(value, out var partnerId)
            ? partnerId
            : throw new FormatException("A partner ID must be a non-empty string (CC-WHS-002).");

    public bool Equals(PartnerId other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is PartnerId other && Equals(other);

    public override int GetHashCode() => _value?.GetHashCode(StringComparison.Ordinal) ?? 0;

    public static bool operator ==(PartnerId left, PartnerId right) => left.Equals(right);

    public static bool operator !=(PartnerId left, PartnerId right) => !left.Equals(right);

    public override string ToString() => Value;
}
