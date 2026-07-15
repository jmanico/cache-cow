namespace CacheCow.Modules.WholesaleB2B.PriceLists;

/// <summary>
/// Wholesale payment terms (CC-WHS-004): net-<see cref="NetDays"/> days.
/// The platform default is net-60 (ratified 2026-07-15), adjustable per
/// partner. No bounds on the adjustment are specified anywhere, so validation
/// requires only a positive day count (flagged in issue 050 reporting).
/// </summary>
public readonly struct PaymentTerms : IEquatable<PaymentTerms>
{
    private readonly int _netDays;

    private PaymentTerms(int netDays)
    {
        _netDays = netDays;
    }

    /// <summary>Days until payment is due. Throws on an uninitialized value (fail closed, never a silent net-0).</summary>
    public int NetDays =>
        _netDays > 0
            ? _netDays
            : throw new WholesaleValidationException(
                "Uninitialized PaymentTerms value; construct via Net or use Net60Default (CC-WHS-004).");

    /// <summary>The ratified platform default: net-60 (CC-WHS-004; decision record 2026-07-15).</summary>
    public static PaymentTerms Net60Default { get; } = Net(60);

    public static PaymentTerms Net(int days) =>
        days > 0
            ? new PaymentTerms(days)
            : throw new WholesaleValidationException(
                "Payment terms require a positive number of net days (CC-WHS-004).");

    public bool Equals(PaymentTerms other) => _netDays == other._netDays;

    public override bool Equals(object? obj) => obj is PaymentTerms other && Equals(other);

    public override int GetHashCode() => _netDays;

    public static bool operator ==(PaymentTerms left, PaymentTerms right) => left.Equals(right);

    public static bool operator !=(PaymentTerms left, PaymentTerms right) => !left.Equals(right);

    public override string ToString() => $"net-{NetDays}";
}
