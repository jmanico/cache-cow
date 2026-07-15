namespace CacheCow.Modules.Invoicing.Invoices;

/// <summary>
/// Opaque reference to a consumer account in the Identity &amp; Access bounded
/// context. Guest orders carry none (CC-ORD-001): a guest invoice has no
/// account and is reachable only via the CC-ORD-010 capability token
/// (SECURITY.md, Authentication rule 14).
/// </summary>
public readonly struct AccountReference : IEquatable<AccountReference>
{
    private readonly string? _value;

    private AccountReference(string value)
    {
        _value = value;
    }

    public string Value =>
        _value ?? throw new InvalidOperationException(
            "Uninitialized AccountReference; use Parse or TryParse.");

    public static bool TryParse(string? value, out AccountReference accountReference)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            accountReference = default;
            return false;
        }

        accountReference = new AccountReference(value);
        return true;
    }

    public static AccountReference Parse(string value) =>
        TryParse(value, out var accountReference)
            ? accountReference
            : throw new FormatException("An account reference must be a non-empty string.");

    public bool Equals(AccountReference other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is AccountReference other && Equals(other);

    public override int GetHashCode() => _value?.GetHashCode(StringComparison.Ordinal) ?? 0;

    public static bool operator ==(AccountReference left, AccountReference right) => left.Equals(right);

    public static bool operator !=(AccountReference left, AccountReference right) => !left.Equals(right);

    public override string ToString() => Value;
}
