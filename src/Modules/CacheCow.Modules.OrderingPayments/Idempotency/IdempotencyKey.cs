namespace CacheCow.Modules.OrderingPayments.Idempotency;

/// <summary>
/// A client-issued idempotency key (CC-ORD-005, CC-API-005). The key string is
/// attacker-controlled input at the HTTP boundary (SECURITY.md, Input
/// validation rule 1): it is validated for shape here and grants nothing by
/// itself — every lookup is by (server-derived scope, key) with a stored
/// request fingerprint (CC-SEC-015).
/// </summary>
public readonly record struct IdempotencyKey
{
    /// <summary>Bound on attacker-controlled input; generous for UUIDs/ULIDs and prefixed keys.</summary>
    public const int MaxLength = 255;

    private readonly string? _value;

    private IdempotencyKey(string value)
    {
        _value = value;
    }

    public string Value =>
        _value ?? throw new InvalidOperationException("Uninitialized IdempotencyKey; use Parse or TryParse.");

    public static bool TryParse(string? value, out IdempotencyKey key)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > MaxLength
            || value.Any(char.IsControl))
        {
            key = default;
            return false;
        }

        key = new IdempotencyKey(value);
        return true;
    }

    /// <summary>Rejects empty, oversized, or control-character keys — reject, never coerce (SECURITY.md, Input validation rule 1).</summary>
    public static IdempotencyKey Parse(string value) =>
        TryParse(value, out var key)
            ? key
            : throw new FormatException(
                $"An idempotency key must be 1..{MaxLength} printable characters (CC-API-005).");

    public override string ToString() => Value;
}
