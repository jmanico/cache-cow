namespace CacheCow.SharedKernel;

/// <summary>Base for all fail-closed money failures (CC-PRC-003).</summary>
public abstract class MoneyException : Exception
{
    protected MoneyException(string message)
        : base(message)
    {
    }
}

/// <summary>Invalid construction: unsupported currency, invalid precision, uninitialized value.</summary>
public sealed class InvalidMoneyException : MoneyException
{
    public InvalidMoneyException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Arithmetic attempted across currencies. No implicit FX conversion exists
/// (CC-PRC-001: no runtime FX conversion of consumer prices).
/// </summary>
public sealed class CurrencyMismatchException : MoneyException
{
    public CurrencyMismatchException(Currency left, Currency right)
        : base($"Cannot combine {left.Code} with {right.Code}; cross-currency arithmetic is not defined (CC-PRC-001).")
    {
    }
}

/// <summary>
/// A monetary computation exceeded the representable range. The operation
/// aborts; no wrapped or saturated result is ever produced (CC-PRC-003, CWE-190).
/// </summary>
public sealed class MoneyOverflowException : MoneyException
{
    public MoneyOverflowException(string operation)
        : base($"Monetary {operation} exceeded the representable range; failing closed (CC-PRC-003).")
    {
    }
}
