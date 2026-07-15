namespace CacheCow.SharedKernel;

/// <summary>
/// A commercial region with its own catalog, currency, tax rules, and compliance
/// regime (REQUIREMENTS.md §2). Exactly the six launch markets exist (CC-MKT-001).
/// Independent of <see cref="Locale"/>: a user MAY shop the DE market in English,
/// and neither type converts to the other (CC-MKT-002, CC-SEC-012).
/// </summary>
public readonly struct Market : IEquatable<Market>
{
    public static readonly Market US = new("US");
    public static readonly Market ES = new("ES");
    public static readonly Market MX = new("MX");
    public static readonly Market DE = new("DE");
    public static readonly Market JP = new("JP");
    public static readonly Market IN = new("IN");

    private static readonly Market[] Launch = [US, ES, MX, DE, JP, IN];

    public static IReadOnlyList<Market> All => Launch;

    private readonly string? _code;

    private Market(string code)
    {
        _code = code;
    }

    public string Code =>
        _code ?? throw new InvalidOperationException(
            "Uninitialized Market value; use the static members or Parse.");

    /// <summary>
    /// Parses an exact launch-market code ("US", "ES", "MX", "DE", "JP", "IN").
    /// Anything else — unknown codes, empty, wrong case — is rejected, never
    /// coerced or defaulted (CC-MKT-001; SECURITY.md, Input validation rule 1).
    /// </summary>
    public static bool TryParse(string? code, out Market market)
    {
        foreach (var candidate in Launch)
        {
            if (string.Equals(candidate.Code, code, StringComparison.Ordinal))
            {
                market = candidate;
                return true;
            }
        }

        market = default;
        return false;
    }

    public static Market Parse(string code) =>
        TryParse(code, out var market)
            ? market
            : throw new FormatException($"'{code}' is not a launch market (CC-MKT-001).");

    public bool Equals(Market other) => string.Equals(_code, other._code, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is Market other && Equals(other);

    public override int GetHashCode() => _code?.GetHashCode(StringComparison.Ordinal) ?? 0;

    public static bool operator ==(Market left, Market right) => left.Equals(right);

    public static bool operator !=(Market left, Market right) => !left.Equals(right);

    public override string ToString() => Code;
}
