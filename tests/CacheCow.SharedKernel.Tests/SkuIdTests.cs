using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.SharedKernel.Tests;

public sealed class SkuIdTests
{
    [Fact]
    [Requirement("CC-CAT-001")]
    public void Equality_is_by_value()
    {
        Assert.Equal(SkuId.Parse("BRISKET-01"), SkuId.Parse("BRISKET-01"));
        Assert.NotEqual(SkuId.Parse("BRISKET-01"), SkuId.Parse("brisket-01"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Empty_ids_are_rejected(string? value)
    {
        Assert.False(SkuId.TryParse(value, out _));
        Assert.Throws<FormatException>(() => SkuId.Parse(value!));
    }

    [Fact]
    public void Round_trips_its_value()
    {
        var id = SkuId.Parse("PANEER-TIKKA-500G");

        Assert.Equal("PANEER-TIKKA-500G", id.Value);
        Assert.Equal("PANEER-TIKKA-500G", id.ToString());
    }

    [Fact]
    public void Uninitialized_sku_id_fails_closed_on_use()
    {
        var uninitialized = default(SkuId);

        Assert.Throws<InvalidOperationException>(() => uninitialized.Value);
    }
}
