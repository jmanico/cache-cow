using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.SharedKernel.Tests;

// CC-PRC-003 / CC-QA-004. All assertions use integer minor units or exact
// decimal — never binary floating point (enforced by the architecture tests).
public sealed class MoneyTests
{
    [Fact]
    [Requirement("CC-PRC-003")]
    public void Stores_integer_minor_units_with_currency()
    {
        var price = Money.FromMinorUnits(14_900, Currency.Usd);

        Assert.Equal(14_900L, price.MinorUnits);
        Assert.Equal(Currency.Usd, price.Currency);
        Assert.Equal(149.00m, price.ToDecimal());
    }

    [Theory]
    [Requirement("CC-PRC-003")]
    [Requirement("CC-QA-004")]
    [InlineData("149.005")]
    [InlineData("0.001")]
    public void Rejects_fractional_minor_units_for_two_decimal_currencies(string amount)
    {
        Assert.Throws<InvalidMoneyException>(() => Money.FromDecimal(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), Currency.Usd));
    }

    [Fact]
    [Requirement("CC-QA-004")]
    public void Jpy_is_zero_decimal_and_rejects_sub_yen_fractions()
    {
        var yen = Money.FromDecimal(14_900m, Currency.Jpy);
        Assert.Equal(14_900L, yen.MinorUnits);
        Assert.Equal(14_900m, yen.ToDecimal());

        Assert.Throws<InvalidMoneyException>(() => Money.FromDecimal(14_900.5m, Currency.Jpy));
    }

    [Fact]
    [Requirement("CC-QA-004")]
    public void Jpy_sums_produce_no_fractional_yen()
    {
        var total = Money.FromDecimal(1_000m, Currency.Jpy) + Money.FromDecimal(2_500m, Currency.Jpy);

        Assert.Equal(3_500L, total.MinorUnits);
        Assert.Equal(3_500m, total.ToDecimal());
    }

    [Fact]
    [Requirement("CC-QA-004")]
    public void Inr_supports_lakh_and_crore_scale_amounts_exactly()
    {
        // ₹12,49,000.00 (DESIGN.md §4.4) and a crore-scale total.
        var lakh = Money.FromDecimal(1_249_000.00m, Currency.Inr);
        Assert.Equal(124_900_000L, lakh.MinorUnits);

        var crore = lakh * 1_000L;
        Assert.Equal(124_900_000_000L, crore.MinorUnits);
        Assert.Equal(1_249_000_000.00m, crore.ToDecimal());
    }

    [Fact]
    [Requirement("CC-PRC-001")]
    public void Cross_currency_arithmetic_fails_closed()
    {
        var dollars = Money.FromMinorUnits(100, Currency.Usd);
        var euros = Money.FromMinorUnits(100, Currency.Eur);

        Assert.Throws<CurrencyMismatchException>(() => dollars + euros);
        Assert.Throws<CurrencyMismatchException>(() => dollars - euros);
        Assert.Throws<CurrencyMismatchException>(() => dollars.CompareTo(euros));
        Assert.False(dollars.Equals(euros));
    }

    [Fact]
    [Requirement("CC-PRC-003")]
    public void Addition_overflow_throws_instead_of_wrapping()
    {
        var max = Money.FromMinorUnits(long.MaxValue, Currency.Inr);
        var one = Money.FromMinorUnits(1, Currency.Inr);

        Assert.Throws<MoneyOverflowException>(() => max + one);
    }

    [Fact]
    [Requirement("CC-PRC-003")]
    public void Subtraction_overflow_throws_instead_of_wrapping()
    {
        var min = Money.FromMinorUnits(long.MinValue, Currency.Usd);
        var one = Money.FromMinorUnits(1, Currency.Usd);

        Assert.Throws<MoneyOverflowException>(() => min - one);
    }

    [Theory]
    [Requirement("CC-PRC-003")]
    [InlineData(long.MaxValue, 2L)]
    [InlineData(long.MaxValue / 2, 3L)]
    [InlineData(1_000_000_000_000L, 10_000_000L)]
    public void Attacker_scale_quantity_multiplication_fails_closed(long unitMinorUnits, long quantity)
    {
        var price = Money.FromMinorUnits(unitMinorUnits, Currency.Jpy);

        Assert.Throws<MoneyOverflowException>(() => price * quantity);
    }

    [Fact]
    [Requirement("CC-QA-004")]
    public void Quantity_times_unit_price_discount_and_total_compute_exactly()
    {
        var unit = Money.FromMinorUnits(14_900, Currency.Usd); // $149.00
        var line = unit * 3L;                                  // $447.00
        var discount = Money.FromMinorUnits(4_700, Currency.Usd);
        var tax = Money.FromMinorUnits(3_538, Currency.Usd);
        var total = line - discount + tax;

        Assert.Equal(44_700L, line.MinorUnits);
        Assert.Equal(43_538L, total.MinorUnits);
    }

    [Fact]
    [Requirement("CC-PRC-003")]
    public void Negating_min_value_fails_closed()
    {
        var min = Money.FromMinorUnits(long.MinValue, Currency.Eur);

        Assert.Throws<MoneyOverflowException>(() => -min);
    }

    [Fact]
    [Requirement("CC-PRC-003")]
    public void Uninitialized_money_fails_closed_on_use()
    {
        var uninitialized = default(Money);

        Assert.Throws<InvalidMoneyException>(() => uninitialized.Currency);
        Assert.Throws<InvalidMoneyException>(() => uninitialized + uninitialized);
    }

    [Fact]
    [Requirement("CC-PRC-001")]
    public void Currency_set_is_exactly_the_five_launch_currencies()
    {
        var codes = Currency.All.Select(c => c.Code).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(["EUR", "INR", "JPY", "MXN", "USD"], codes);
        Assert.False(Currency.TryParse("GBP", out _));
        Assert.False(Currency.TryParse("usd", out _));
        Assert.False(Currency.TryParse(null, out _));
        Assert.Throws<InvalidMoneyException>(() => Currency.Parse("BTC"));
    }

    [Fact]
    public void Comparison_orders_by_amount_within_a_currency()
    {
        var small = Money.FromMinorUnits(100, Currency.Mxn);
        var large = Money.FromMinorUnits(200, Currency.Mxn);

        Assert.True(small < large);
        Assert.True(large >= small);
        Assert.Equal(small, Money.FromMinorUnits(100, Currency.Mxn));
        Assert.True(small != large);
    }
}
