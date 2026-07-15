namespace CacheCow.SharedKernel;

/// <summary>
/// The closed set of launch currencies fixed by CC-PRC-001
/// (US=USD, ES=EUR, MX=MXN, DE=EUR, JP=JPY, IN=INR).
/// </summary>
public sealed class Currency : IEquatable<Currency>
{
    public static readonly Currency Usd = new("USD", 2);
    public static readonly Currency Eur = new("EUR", 2);
    public static readonly Currency Mxn = new("MXN", 2);
    public static readonly Currency Jpy = new("JPY", 0);
    public static readonly Currency Inr = new("INR", 2);

    private static readonly IReadOnlyDictionary<string, Currency> ByCode =
        new Dictionary<string, Currency>(StringComparer.Ordinal)
        {
            [Usd.Code] = Usd,
            [Eur.Code] = Eur,
            [Mxn.Code] = Mxn,
            [Jpy.Code] = Jpy,
            [Inr.Code] = Inr,
        };

    public static IReadOnlyCollection<Currency> All => ByCode.Values.ToArray();

    private Currency(string code, int minorUnitExponent)
    {
        Code = code;
        MinorUnitExponent = minorUnitExponent;
    }

    /// <summary>ISO 4217 alphabetic code.</summary>
    public string Code { get; }

    /// <summary>Digits after the decimal point in major units (JPY is zero-decimal).</summary>
    public int MinorUnitExponent { get; }

    /// <summary>
    /// Parses an exact ISO 4217 code from the launch set. Rejects anything else
    /// (SECURITY.md, Input validation rule 1: reject, never coerce).
    /// </summary>
    public static bool TryParse(string? code, out Currency? currency)
    {
        if (code is not null && ByCode.TryGetValue(code, out var found))
        {
            currency = found;
            return true;
        }

        currency = null;
        return false;
    }

    public static Currency Parse(string code) =>
        TryParse(code, out var currency)
            ? currency!
            : throw new InvalidMoneyException($"'{code}' is not a launch currency (CC-PRC-001).");

    public bool Equals(Currency? other) => ReferenceEquals(this, other);

    public override bool Equals(object? obj) => obj is Currency other && Equals(other);

    public override int GetHashCode() => Code.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Code;
}
