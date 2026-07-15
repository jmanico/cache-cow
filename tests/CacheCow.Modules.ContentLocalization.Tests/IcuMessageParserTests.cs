using CacheCow.Modules.ContentLocalization.Resources.MessageFormat;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.ContentLocalization.Tests;

/// <summary>
/// Issue 064: the first-party ICU MessageFormat subset parser accepts the
/// needed subset (placeholders, plural, select) and rejects everything else —
/// malformed syntax and HTML markup are rejected, never sanitized into
/// acceptance (CC-I18N-002; SECURITY.md, Input validation rules 1 and 7).
/// </summary>
public sealed class IcuMessageParserTests
{
    [Theory]
    [Requirement("CC-I18N-002")]
    [InlineData("Plain text with no arguments.")]
    [InlineData("Hello {name}, welcome back.")]
    [InlineData("{count, plural, one {# item} other {# items}}")]
    [InlineData("{count, plural, =0 {none} one {# item} other {# items}}")]
    [InlineData("{state, select, hit {cache hit} miss {cache miss} other {warming}}")]
    [InlineData("{count, plural, other {{name} has # packs}}")]
    [InlineData("It''s smoked. '{'not an argument'}'")]
    [InlineData("¥1,490 — 合計 {total}")]
    public void Valid_subset_messages_parse(string message)
    {
        Assert.True(IcuMessageParser.TryParse(message, out var parsed, out var error), error);
        Assert.NotNull(parsed);
    }

    [Theory]
    [Requirement("CC-I18N-002")]
    [InlineData("{name")]                                        // unterminated argument
    [InlineData("name}")]                                        // unmatched close brace
    [InlineData("{}")]                                           // empty argument name
    [InlineData("{n, plural}")]                                  // missing style
    [InlineData("{n, plural, one {x}}")]                         // no 'other' branch
    [InlineData("{n, plural, one {x} one {y} other {z}}")]       // duplicate selector
    [InlineData("{n, plural, bogus {x} other {y}}")]             // unknown plural category
    [InlineData("{n, plural, = {x} other {y}}")]                 // '=' without digits
    [InlineData("{n, number}")]                                  // unsupported argument type (subset)
    [InlineData("{x, select, other {unclosed}")]                 // unterminated select
    [InlineData("{na me}")]                                      // junk after name
    [InlineData("It''s '{quoted forever")]                       // unterminated quoted run
    public void Malformed_syntax_is_rejected(string message)
    {
        Assert.False(IcuMessageParser.TryParse(message, out var parsed, out var error));
        Assert.Null(parsed);
        Assert.NotNull(error);
        Assert.Throws<IcuMessageFormatException>(() => IcuMessageParser.Parse(message));
    }

    [Theory]
    [Requirement("CC-I18N-002")]
    [InlineData("Hello <b>{name}</b>")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("1 < 2")]
    [InlineData("{state, select, other {<img src=x onerror=alert(1)>}}")]
    public void Html_markup_in_string_values_is_rejected(string message)
    {
        Assert.False(IcuMessageParser.TryParse(message, out _, out var error));
        Assert.Contains("HTML", error, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-I18N-002")]
    public void Nesting_beyond_the_depth_limit_is_rejected()
    {
        // 10 nested selects (> MaxNestingDepth of 8), all syntactically valid.
        var message = "x";
        for (var i = 0; i < 10; i++)
        {
            message = $"{{s{i}, select, other {{{message}}}}}";
        }

        Assert.False(IcuMessageParser.TryParse(message, out _, out var error));
        Assert.Contains("Nesting", error, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-I18N-002")]
    public void Placeholder_collection_includes_nested_placeholders_with_kinds()
    {
        var parsed = IcuMessageParser.Parse(
            "{count, plural, one {{name} has # pack} other {{name} has # packs}} {state, select, other {ok}}");

        Assert.Equal(
            [
                new IcuPlaceholder("count", IcuPlaceholderKind.Plural),
                new IcuPlaceholder("name", IcuPlaceholderKind.Text),
                new IcuPlaceholder("state", IcuPlaceholderKind.Select),
            ],
            parsed.Placeholders.OrderBy(p => p.Name, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    [Requirement("CC-I18N-002")]
    public void Fuzzing_hostile_syntax_rejects_and_never_crashes()
    {
        string[] corpus =
        [
            "{{{{{{{{{{{{{{{{{{",
            "}}}}}}}}}}",
            "{a,plural,other{{a,plural,other{{a,plural,other{#}}}}}}",
            "{'}'}",
            "'''",
            "''''",
            "{n, plural, =999999999999999999999999 {x} other {y}}",
            "{n , select , }",
            "\0{x}",
            "{x, select, other {}}",
            new string('{', 5000),
            new string('\'', 4999),
        ];

        foreach (var input in corpus)
        {
            // Must return a verdict (accept or structured reject), never throw.
            var accepted = IcuMessageParser.TryParse(input, out var parsed, out var error);
            Assert.True(accepted ? parsed is not null : error is not null);
        }
    }
}
