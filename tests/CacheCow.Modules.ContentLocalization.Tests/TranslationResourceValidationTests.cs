using CacheCow.Modules.ContentLocalization.Resources;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.ContentLocalization.Tests;

/// <summary>
/// Issue 064: the CI-runnable resource validation — exact launch-locale
/// coverage, key parity, no HTML, ICU well-formedness, and placeholder parity
/// across all seven locales (CC-I18N-002; CC-QA-006 gates; SECURITY.md, Input
/// validation rule 7: translation files are untrusted input, five languages
/// means five supply chains for strings).
/// </summary>
public sealed class TranslationResourceValidationTests
{
    private static readonly string[] AllLocaleTags =
        ["en-US", "es-ES", "es-MX", "de-DE", "ja-JP", "en-IN", "hi-IN"];

    /// <summary>A parity-correct set: same keys and placeholders in all seven locales.</summary>
    private static Dictionary<Locale, IReadOnlyDictionary<string, string>> ValidResources()
    {
        var resources = new Dictionary<Locale, IReadOnlyDictionary<string, string>>();
        foreach (var tag in AllLocaleTags)
        {
            resources[Locale.Parse(tag)] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["greeting"] = $"[{tag}] Hello {{name}}",
                ["stock.count"] = $"[{tag}] {{count, plural, one {{# pack}} other {{# packs}}}}",
                ["stock.state"] = $"[{tag}] {{state, select, hit {{hit}} other {{warming}}}}",
            };
        }

        return resources;
    }

    private static TranslationValidationResult Validate(Dictionary<Locale, IReadOnlyDictionary<string, string>> resources) =>
        TranslationResourceValidator.Validate(new TranslationResourceSet(resources));

    private static Dictionary<string, string> Mutate(
        IReadOnlyDictionary<string, string> locale, string key, string newValue)
    {
        var copy = new Dictionary<string, string>(locale, StringComparer.Ordinal)
        {
            [key] = newValue,
        };
        return copy;
    }

    [Fact]
    [Requirement("CC-I18N-002")]
    public void A_parity_correct_seven_locale_set_is_valid()
    {
        var result = Validate(ValidResources());

        Assert.True(result.IsValid, string.Join('\n', result.Violations.Select(v => v.Detail)));
    }

    [Theory]
    [Requirement("CC-I18N-002")]
    [InlineData("en-US")]
    [InlineData("es-ES")]
    [InlineData("es-MX")]
    [InlineData("de-DE")]
    [InlineData("ja-JP")]
    [InlineData("en-IN")]
    [InlineData("hi-IN")]
    public void A_key_missing_in_any_single_locale_fails_key_parity(string localeTag)
    {
        var resources = ValidResources();
        var locale = Locale.Parse(localeTag);
        var withoutKey = new Dictionary<string, string>(resources[locale], StringComparer.Ordinal);
        withoutKey.Remove("greeting");
        resources[locale] = withoutKey;

        var result = Validate(resources);

        Assert.False(result.IsValid);
        var violation = Assert.Single(result.Violations, v => v.Rule == TranslationViolationRule.MissingKey);
        Assert.Equal("greeting", violation.Key);
        Assert.Equal(localeTag, violation.LocaleTag);
    }

    [Fact]
    [Requirement("CC-I18N-002")]
    public void A_launch_locale_missing_entirely_is_a_violation()
    {
        var resources = ValidResources();
        resources.Remove(Locale.Parse("hi-IN"));

        var result = Validate(resources);

        Assert.Contains(result.Violations, v =>
            v.Rule == TranslationViolationRule.MissingLocale && v.LocaleTag == "hi-IN");
    }

    [Fact]
    [Requirement("CC-I18N-002")]
    public void A_locale_outside_the_launch_set_is_rejected_not_accepted_provisionally()
    {
        var resources = ValidResources();
        resources[Locale.Parse("fr-FR")] = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["greeting"] = "Bonjour {name}",
        };

        var result = Validate(resources);

        Assert.Contains(result.Violations, v =>
            v.Rule == TranslationViolationRule.UnexpectedLocale && v.LocaleTag == "fr-FR");
    }

    [Fact]
    [Requirement("CC-I18N-002")]
    public void Html_in_any_locale_string_fails_the_build()
    {
        var resources = ValidResources();
        var deDe = Locale.Parse("de-DE");
        resources[deDe] = Mutate(resources[deDe], "greeting", "Hallo <b>{name}</b>");

        var result = Validate(resources);

        Assert.False(result.IsValid);
        var violation = Assert.Single(result.Violations, v => v.Rule == TranslationViolationRule.HtmlInResource);
        Assert.Equal("greeting", violation.Key);
        Assert.Equal("de-DE", violation.LocaleTag);
        // The raw resource value is never echoed into the failure output
        // (SECURITY.md, Logging rule 5).
        Assert.DoesNotContain("<b>", violation.Detail, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-I18N-002")]
    public void Malformed_icu_in_any_locale_is_rejected_with_key_and_locale_named()
    {
        var resources = ValidResources();
        var jaJp = Locale.Parse("ja-JP");
        resources[jaJp] = Mutate(resources[jaJp], "stock.count", "{count, plural, one {# pack}}"); // no 'other'

        var result = Validate(resources);

        Assert.False(result.IsValid);
        var violation = Assert.Single(result.Violations, v => v.Rule == TranslationViolationRule.MalformedMessage);
        Assert.Equal("stock.count", violation.Key);
        Assert.Equal("ja-JP", violation.LocaleTag);
    }

    [Fact]
    [Requirement("CC-I18N-002")]
    public void Placeholder_name_mismatch_across_locales_is_detected()
    {
        var resources = ValidResources();
        var esMx = Locale.Parse("es-MX");
        resources[esMx] = Mutate(resources[esMx], "greeting", "[es-MX] Hola {nombre}");

        var result = Validate(resources);

        Assert.False(result.IsValid);
        var violation = Assert.Single(result.Violations, v => v.Rule == TranslationViolationRule.PlaceholderMismatch);
        Assert.Equal("greeting", violation.Key);
        Assert.Equal("es-MX", violation.LocaleTag);
        Assert.Contains("en-US", violation.Detail, StringComparison.Ordinal); // offending locales identified (AC-04)
    }

    [Fact]
    [Requirement("CC-I18N-002")]
    public void Placeholder_kind_mismatch_is_detected_even_when_the_name_matches()
    {
        var resources = ValidResources();
        var enIn = Locale.Parse("en-IN");
        // 'count' exists but as a text placeholder instead of a plural.
        resources[enIn] = Mutate(resources[enIn], "stock.count", "[en-IN] {count} packs");

        var result = Validate(resources);

        Assert.Contains(result.Violations, v =>
            v.Rule == TranslationViolationRule.PlaceholderMismatch && v.LocaleTag == "en-IN" && v.Key == "stock.count");
    }

    [Fact]
    [Requirement("CC-I18N-002")]
    public void Extra_placeholder_in_a_translation_is_detected()
    {
        var resources = ValidResources();
        var esEs = Locale.Parse("es-ES");
        resources[esEs] = Mutate(resources[esEs], "greeting", "[es-ES] Hola {name} {extra}");

        var result = Validate(resources);

        Assert.Contains(result.Violations, v =>
            v.Rule == TranslationViolationRule.PlaceholderMismatch && v.LocaleTag == "es-ES");
    }

    [Fact]
    [Requirement("CC-I18N-002")]
    public void An_invalid_set_cannot_become_a_registry()
    {
        var resources = ValidResources();
        var enUs = Locale.Parse("en-US");
        resources[enUs] = Mutate(resources[enUs], "greeting", "Hello <script>{name}</script>");

        var exception = Assert.Throws<TranslationValidationException>(
            () => StringResourceRegistry.Create(new TranslationResourceSet(resources)));

        Assert.NotEmpty(exception.Violations);
    }
}
