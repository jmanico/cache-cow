using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.SharedKernel.Tests;

public sealed class LocaleTests
{
    [Theory]
    [Requirement("CC-I18N-001")]
    [InlineData("en-US")]
    [InlineData("es-ES")]
    [InlineData("es-MX")]
    [InlineData("de-DE")]
    [InlineData("ja-JP")]
    [InlineData("en-IN")]
    [InlineData("hi-IN")]
    public void Launch_locales_parse_and_round_trip(string tag)
    {
        Assert.True(Locale.TryParse(tag, out var locale));
        Assert.Equal(tag, locale.Tag);
    }

    [Theory]
    [Requirement("CC-I18N-001")]
    [InlineData("EN-us", "en-US")]
    [InlineData("HI-in", "hi-IN")]
    [InlineData("zh-hans-cn", "zh-Hans-CN")]
    public void Well_formed_tags_canonicalize_case(string input, string canonical)
    {
        var locale = Locale.Parse(input);

        Assert.Equal(canonical, locale.Tag);
        Assert.Equal(locale, Locale.Parse(canonical));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("e")]
    [InlineData("en_US")]
    [InlineData("en-")]
    [InlineData("-US")]
    [InlineData("en--US")]
    [InlineData("1234")]
    [InlineData("en US")]
    [InlineData("en-US-x-priv")]
    [InlineData("english")]
    [InlineData("<script>")]
    [InlineData("en-US;q=0.9")]
    public void Malformed_tags_are_rejected(string? tag)
    {
        Assert.False(Locale.TryParse(tag, out _));
        Assert.Throws<FormatException>(() => Locale.Parse(tag!));
    }

    [Fact]
    public void Region_only_and_numeric_regions_are_supported_syntax()
    {
        Assert.True(Locale.TryParse("es-419", out var latam));
        Assert.Equal("es-419", latam.Tag);

        Assert.True(Locale.TryParse("de", out var german));
        Assert.Equal("de", german.Tag);
    }

    [Fact]
    public void Equality_is_by_canonical_value()
    {
        Assert.Equal(Locale.Parse("EN-us"), Locale.Parse("en-US"));
        Assert.NotEqual(Locale.Parse("en-US"), Locale.Parse("en-IN"));
    }

    [Fact]
    public void Uninitialized_locale_fails_closed_on_use()
    {
        var uninitialized = default(Locale);

        Assert.Throws<InvalidOperationException>(() => uninitialized.Tag);
    }
}
