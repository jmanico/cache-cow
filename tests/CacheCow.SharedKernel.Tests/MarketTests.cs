using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.SharedKernel.Tests;

public sealed class MarketTests
{
    [Fact]
    [Requirement("CC-MKT-001")]
    public void Launch_markets_are_exactly_the_six()
    {
        var codes = Market.All.Select(m => m.Code).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(["DE", "ES", "IN", "JP", "MX", "US"], codes);
    }

    [Theory]
    [Requirement("CC-MKT-001")]
    [InlineData("FR")]
    [InlineData("XX")]
    [InlineData("")]
    [InlineData("us")]
    [InlineData("Us")]
    [InlineData(" US")]
    [InlineData("USA")]
    [InlineData(null)]
    public void Non_launch_or_malformed_codes_are_rejected(string? code)
    {
        Assert.False(Market.TryParse(code, out _));
        Assert.Throws<FormatException>(() => Market.Parse(code!));
    }

    [Fact]
    [Requirement("CC-MKT-001")]
    public void Parsing_round_trips_launch_codes()
    {
        foreach (var market in Market.All)
        {
            Assert.True(Market.TryParse(market.Code, out var parsed));
            Assert.Equal(market, parsed);
            Assert.Equal(market.Code, parsed.ToString());
        }
    }

    [Fact]
    [Requirement("CC-MKT-002")]
    public void Market_and_locale_are_type_level_independent()
    {
        // No implicit or explicit conversion exists between Market and Locale;
        // a Locale can never be used where a Market is required (CC-SEC-012).
        Assert.False(typeof(Market).IsAssignableFrom(typeof(Locale)));
        Assert.False(typeof(Locale).IsAssignableFrom(typeof(Market)));
        Assert.DoesNotContain(
            typeof(Market).GetMethods(),
            m => m.Name is "op_Implicit" or "op_Explicit");
        Assert.DoesNotContain(
            typeof(Locale).GetMethods(),
            m => m.Name is "op_Implicit" or "op_Explicit");
    }

    [Fact]
    public void Equality_is_by_value()
    {
        Assert.Equal(Market.IN, Market.Parse("IN"));
        Assert.NotEqual(Market.IN, Market.US);
        Assert.True(Market.DE == Market.Parse("DE"));
    }

    [Fact]
    public void Uninitialized_market_fails_closed_on_use()
    {
        var uninitialized = default(Market);

        Assert.Throws<InvalidOperationException>(() => uninitialized.Code);
    }
}
