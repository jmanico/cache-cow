using CacheCow.Modules.ContentLocalization.Resources.MessageFormat;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.ContentLocalization.Tests;

/// <summary>
/// Issue 064: formatting is escape-by-default — interpolated values are
/// inserted as plain text, never re-parsed as ICU syntax and never
/// interpreted as markup, with no opt-out to raw HTML anywhere in the
/// pipeline (SECURITY.md, Input validation rules 5 and 7; issue 064 AC-05).
/// </summary>
public sealed class IcuMessageFormattingTests
{
    private static readonly Locale EnUs = Locale.Parse("en-US");

    private static Dictionary<string, object?> Args(string name, object? value) =>
        new(StringComparer.Ordinal) { [name] = value };

    [Fact]
    [Requirement("CC-I18N-002")]
    public void Interpolated_script_payload_renders_inert_as_text()
    {
        var message = IcuMessageParser.Parse("Hello {name}!");

        var formatted = message.Format(EnUs, Args("name", "<script>alert(1)</script>"));

        // The hostile value appears verbatim as inert plain text: the
        // formatter never interprets it, and because resources themselves can
        // never contain '<', any '<' in output provably came from a value.
        Assert.Equal("Hello <script>alert(1)</script>!", formatted);
    }

    [Fact]
    [Requirement("CC-I18N-002")]
    public void Interpolated_values_are_never_reparsed_as_icu_syntax()
    {
        var message = IcuMessageParser.Parse("Hello {name}!");

        var formatted = message.Format(EnUs, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = "{other} {count, plural, other {#}}",
            ["other"] = "SHOULD NEVER APPEAR",
        });

        Assert.Equal("Hello {other} {count, plural, other {#}}!", formatted);
    }

    [Theory]
    [Requirement("CC-I18N-002")]
    [InlineData("en-US", 1, "1 pack")]
    [InlineData("en-US", 2, "2 packs")]
    [InlineData("de-DE", 1, "1 pack")]
    [InlineData("es-ES", 5, "5 packs")]
    [InlineData("ja-JP", 1, "1 packs")] // Japanese is 'other'-only in CLDR
    [InlineData("hi-IN", 0, "0 pack")]  // Hindi: n == 0 or 1 → one
    [InlineData("hi-IN", 1, "1 pack")]
    [InlineData("hi-IN", 2, "2 packs")]
    public void Plural_category_selection_follows_the_locale(string localeTag, int count, string expected)
    {
        var message = IcuMessageParser.Parse("{count, plural, one {# pack} other {# packs}}");

        var formatted = message.Format(Locale.Parse(localeTag), Args("count", count));

        Assert.Equal(expected, formatted);
    }

    [Fact]
    [Requirement("CC-I18N-002")]
    public void Exact_match_wins_over_plural_category()
    {
        var message = IcuMessageParser.Parse("{count, plural, =0 {empty cache} one {# pack} other {# packs}}");

        Assert.Equal("empty cache", message.Format(EnUs, Args("count", 0)));
        Assert.Equal("1 pack", message.Format(EnUs, Args("count", 1)));
    }

    [Theory]
    [Requirement("CC-I18N-002")]
    [InlineData("en-US", "1,234 packs")]
    [InlineData("de-DE", "1.234 packs")]
    public void Pound_operand_is_locale_formatted(string localeTag, string expected)
    {
        var message = IcuMessageParser.Parse("{count, plural, one {# pack} other {# packs}}");

        Assert.Equal(expected, message.Format(Locale.Parse(localeTag), Args("count", 1234)));
    }

    [Fact]
    [Requirement("CC-I18N-002")]
    public void Select_chooses_the_branch_and_falls_back_to_other()
    {
        var message = IcuMessageParser.Parse("{state, select, hit {cache hit} miss {cache miss} other {warming}}");

        Assert.Equal("cache hit", message.Format(EnUs, Args("state", "hit")));
        Assert.Equal("warming", message.Format(EnUs, Args("state", "unknown-state")));
    }

    [Fact]
    [Requirement("CC-I18N-002")]
    public void Hostile_select_value_is_never_emitted()
    {
        var message = IcuMessageParser.Parse("{state, select, other {warming}}");

        var formatted = message.Format(EnUs, Args("state", "<img onerror=alert(1)>"));

        Assert.Equal("warming", formatted);
    }

    [Fact]
    [Requirement("CC-I18N-002")]
    [Requirement("CC-I18N-006")]
    public void Missing_argument_fails_closed_instead_of_rendering_a_broken_message()
    {
        var message = IcuMessageParser.Parse("Hello {name}!");

        Assert.Throws<IcuMessageArgumentException>(
            () => message.Format(EnUs, new Dictionary<string, object?>(StringComparer.Ordinal)));
    }

    [Fact]
    [Requirement("CC-I18N-002")]
    public void Non_numeric_plural_argument_is_rejected()
    {
        var message = IcuMessageParser.Parse("{count, plural, other {# packs}}");

        Assert.Throws<IcuMessageArgumentException>(() => message.Format(EnUs, Args("count", "not-a-number")));
    }
}
