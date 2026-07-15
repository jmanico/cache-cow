namespace CacheCow.SharedKernel;

/// <summary>
/// Currency-aware monetary value stored as integer minor units (CC-PRC-003;
/// ARCHITECTURE.md, Dependency rule 9). All arithmetic is overflow-checked and
/// fails closed rather than wrapping. The type exposes no binary floating-point
/// view: amounts round-trip only via <see cref="MinorUnits"/> (long) and
/// <see cref="ToDecimal"/> (exact decimal). Display formatting is out of scope
/// (CC-PRC-004, issue 034).
/// </summary>
public readonly struct Money : IEquatable<Money>, IComparable<Money>
{
    private readonly Currency? _currency;

    private Money(long minorUnits, Currency currency)
    {
        MinorUnits = minorUnits;
        _currency = currency;
    }

    /// <summary>The amount in the currency's minor units (yen for JPY, cents for USD…).</summary>
    public long MinorUnits { get; }

    public Currency Currency =>
        _currency ?? throw new InvalidMoneyException(
            "Uninitialized Money value; construct via FromMinorUnits or FromDecimal.");

    public static Money FromMinorUnits(long minorUnits, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);
        return new Money(minorUnits, currency);
    }

    /// <summary>
    /// Constructs from an exact decimal major-unit amount. Rejects amounts with
    /// fractional minor units for the currency (e.g., sub-yen JPY) instead of
    /// rounding them into acceptance (CC-PRC-003; no rounding policy is ratified).
    /// </summary>
    public static Money FromDecimal(decimal majorUnits, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);

        decimal scaled;
        try
        {
            scaled = majorUnits * Pow10(currency.MinorUnitExponent);
        }
        catch (OverflowException)
        {
            throw new MoneyOverflowException("construction");
        }

        if (decimal.Truncate(scaled) != scaled)
        {
            throw new InvalidMoneyException(
                $"{majorUnits} has fractional minor units for {currency.Code} (CC-PRC-003).");
        }

        if (scaled > long.MaxValue || scaled < long.MinValue)
        {
            throw new MoneyOverflowException("construction");
        }

        return new Money((long)scaled, currency);
    }

    /// <summary>Exact major-unit view (decimal is exact for the full long range; never binary floating point).</summary>
    public decimal ToDecimal() => MinorUnits / Pow10(Currency.MinorUnitExponent);

    public bool IsSameCurrencyAs(Money other) => Currency.Equals(other.Currency);

    public Money Add(Money other)
    {
        RequireSameCurrency(other);
        try
        {
            return new Money(checked(MinorUnits + other.MinorUnits), Currency);
        }
        catch (OverflowException)
        {
            throw new MoneyOverflowException("addition");
        }
    }

    public Money Subtract(Money other)
    {
        RequireSameCurrency(other);
        try
        {
            return new Money(checked(MinorUnits - other.MinorUnits), Currency);
        }
        catch (OverflowException)
        {
            throw new MoneyOverflowException("subtraction");
        }
    }

    /// <summary>
    /// Quantity × unit price. Quantities are attacker-influenced (CC-PRC-003):
    /// the multiplication is overflow-checked and fails closed.
    /// </summary>
    public Money MultiplyBy(long quantity)
    {
        try
        {
            return new Money(checked(MinorUnits * quantity), Currency);
        }
        catch (OverflowException)
        {
            throw new MoneyOverflowException("multiplication");
        }
    }

    public Money Negate()
    {
        try
        {
            return new Money(checked(-MinorUnits), Currency);
        }
        catch (OverflowException)
        {
            throw new MoneyOverflowException("negation");
        }
    }

    public static Money operator +(Money left, Money right) => left.Add(right);

    public static Money operator -(Money left, Money right) => left.Subtract(right);

    public static Money operator *(Money price, long quantity) => price.MultiplyBy(quantity);

    public static Money operator *(long quantity, Money price) => price.MultiplyBy(quantity);

    public static Money operator -(Money value) => value.Negate();

    public bool Equals(Money other) =>
        Currency.Equals(other.Currency) && MinorUnits == other.MinorUnits;

    public override bool Equals(object? obj) => obj is Money other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Currency, MinorUnits);

    public int CompareTo(Money other)
    {
        RequireSameCurrency(other);
        return MinorUnits.CompareTo(other.MinorUnits);
    }

    public static bool operator ==(Money left, Money right) => left.Equals(right);

    public static bool operator !=(Money left, Money right) => !left.Equals(right);

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"{MinorUnits} {Currency.Code} (minor units)";

    private void RequireSameCurrency(Money other)
    {
        if (!Currency.Equals(other.Currency))
        {
            throw new CurrencyMismatchException(Currency, other.Currency);
        }
    }

    private static decimal Pow10(int exponent)
    {
        var result = 1m;
        for (var i = 0; i < exponent; i++)
        {
            result *= 10m;
        }

        return result;
    }
}
