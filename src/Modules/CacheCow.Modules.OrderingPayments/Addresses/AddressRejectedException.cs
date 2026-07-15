namespace CacheCow.Modules.OrderingPayments.Addresses;

/// <summary>Why one submitted address field (or the submission as a whole) was rejected (CC-ORD-002).</summary>
public enum AddressRejectionCode
{
    /// <summary>No address schema is configured for the transacting market — fail closed, never validate against a guessed format (SECURITY.md, Logging rule 2).</summary>
    MarketNotConfigured = 0,

    /// <summary>The field is not declared by the market's schema (SECURITY.md, Input validation rule 2: reject unknown fields).</summary>
    UnknownField = 1,

    /// <summary>A schema-required field is absent or blank.</summary>
    MissingRequiredField = 2,

    /// <summary>An optional field was submitted with a blank value — rejected, never silently dropped (reject-not-sanitize).</summary>
    BlankValue = 3,

    /// <summary>The value contains control characters (log-injection and downstream-pipeline defense; SECURITY.md, Logging rule 5).</summary>
    InvalidCharacters = 4,

    /// <summary>The value exceeds the field's length bound (attacker-controlled input; SECURITY.md, HTTP boundary rule 7 spirit).</summary>
    ValueTooLong = 5,

    /// <summary>The value fails the field's format pattern (e.g. IN PIN code, JP postal code).</summary>
    InvalidFormat = 6,
}

/// <summary>One rejected field, identified generically: field name and code only — never the submitted value, which is untrusted PII (SECURITY.md, Logging rules 4–5).</summary>
public sealed record AddressFieldError(string Field, AddressRejectionCode Code);

/// <summary>
/// Typed rejection of an address submission (issue 038; CC-ORD-002). Maps to
/// HTTP 400 with RFC 9457 problem details at the API surface (issue 021),
/// identifying invalid fields generically. No address is produced and no
/// order processing occurs when this is thrown. The message and errors carry
/// field names and codes only — submitted values never appear
/// (SECURITY.md, Logging rules 1, 4–5).
/// </summary>
public sealed class AddressRejectedException : Exception
{
    public AddressRejectedException(string marketCode, IReadOnlyList<AddressFieldError> errors)
        : base(
            $"Address rejected for market '{marketCode}': "
            + string.Join(", ", (errors ?? []).Select(error => $"{error.Field}:{error.Code}"))
            + " (CC-ORD-002; SECURITY.md, Input validation rule 1).")
    {
        ArgumentNullException.ThrowIfNull(errors);
        MarketCode = marketCode;
        Errors = errors;
    }

    public string MarketCode { get; }

    public IReadOnlyList<AddressFieldError> Errors { get; }
}
