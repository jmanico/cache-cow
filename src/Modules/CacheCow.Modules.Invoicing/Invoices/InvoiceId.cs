using System.Security.Cryptography;

namespace CacheCow.Modules.Invoicing.Invoices;

/// <summary>
/// Opaque, unguessable technical identity of an invoice record — distinct from
/// the sequential, legally required <see cref="InvoiceNumber"/>. Generated from
/// 128 bits of cryptographic randomness so no externally reachable identifier
/// for an invoice is ever guessable or enumerable (CC-INV-002: "the link MUST
/// NOT resolve to a guessable order identifier"; CC-ORD-010; issue 048, AC-06).
/// The sequential invoice number exists for legal numbering only and never
/// appears in access paths or links.
/// </summary>
public readonly struct InvoiceId : IEquatable<InvoiceId>
{
    private const int EntropyBytes = 16; // 128 bits (SECURITY.md, Authentication rule 14 floor)

    private readonly string? _value;

    private InvoiceId(string value)
    {
        _value = value;
    }

    public string Value =>
        _value ?? throw new InvalidOperationException(
            "Uninitialized InvoiceId; use NewId or TryParse.");

    /// <summary>Creates a new identity from 128 bits of cryptographic randomness.</summary>
    public static InvoiceId NewId()
    {
        var bytes = RandomNumberGenerator.GetBytes(EntropyBytes);
        return new InvoiceId(Convert.ToHexStringLower(bytes));
    }

    public static bool TryParse(string? value, out InvoiceId invoiceId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            invoiceId = default;
            return false;
        }

        invoiceId = new InvoiceId(value);
        return true;
    }

    public bool Equals(InvoiceId other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is InvoiceId other && Equals(other);

    public override int GetHashCode() => _value?.GetHashCode(StringComparison.Ordinal) ?? 0;

    public static bool operator ==(InvoiceId left, InvoiceId right) => left.Equals(right);

    public static bool operator !=(InvoiceId left, InvoiceId right) => !left.Equals(right);

    public override string ToString() => Value;
}
