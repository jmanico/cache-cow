using CacheCow.Modules.ContentLocalization.Email;
using CacheCow.Modules.ContentLocalization.Resources;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.ContentLocalization.Tests;

/// <summary>
/// Issue 064/043: format-time fallback follows the market's primary language
/// and never renders a broken template (CC-I18N-006). The IN primary
/// (en-IN vs hi-IN) is an open decision: no default exists, and using it
/// unconfigured fails closed (CLAUDE.md: never resolve an open decision).
/// </summary>
public sealed class StringResourceFallbackTests
{
    private static readonly StringResourceRegistry Registry =
        StringResourceRegistry.Create(PlaceholderOrderEmailResources.Set);

    private static readonly Dictionary<string, object?> Reference =
        new(StringComparer.Ordinal) { ["orderReference"] = "CC-1042" };

    [Fact]
    [Requirement("CC-I18N-002")]
    public void The_registry_serves_the_requested_key_locale_pair()
    {
        var formatter = new LocalizedMessageFormatter(Registry, MarketPrimaryLocales.Default);

        var (text, used) = formatter.Format(
            OrderEmailResourceKeys.ConfirmationSubject, Locale.Parse("ja-JP"), Market.JP, Reference);

        Assert.Equal(Locale.Parse("ja-JP"), used);
        Assert.Equal("ご注文 CC-1042 を承りました", text);
    }

    [Fact]
    [Requirement("CC-I18N-006")]
    public void A_locale_without_resources_falls_back_to_the_markets_primary_language()
    {
        var formatter = new LocalizedMessageFormatter(Registry, MarketPrimaryLocales.Default);

        // fr-FR is a well-formed locale with no resources; the DE market's
        // primary language is de-DE (derived from CC-I18N-001).
        var (text, used) = formatter.Format(
            OrderEmailResourceKeys.ConfirmationSubject, Locale.Parse("fr-FR"), Market.DE, Reference);

        Assert.Equal(Locale.Parse("de-DE"), used);
        Assert.Equal("Bestellung CC-1042 bestätigt", text);
    }

    [Theory]
    [Requirement("CC-I18N-006")]
    [InlineData("US", "en-US")]
    [InlineData("ES", "es-ES")]
    [InlineData("MX", "es-MX")]
    [InlineData("DE", "de-DE")]
    [InlineData("JP", "ja-JP")]
    public void The_unambiguous_market_primary_languages_derive_from_the_launch_locales(string marketCode, string expectedTag)
    {
        Assert.Equal(
            Locale.Parse(expectedTag),
            MarketPrimaryLocales.Default.GetPrimaryLocale(Market.Parse(marketCode)));
    }

    [Fact]
    [Requirement("CC-I18N-006")]
    public void The_india_primary_language_has_no_default_and_fails_closed()
    {
        // en-IN vs hi-IN is an open decision (issue 043/064 open questions);
        // guessing here would resolve it in code, which CLAUDE.md forbids.
        Assert.False(MarketPrimaryLocales.Default.TryGetPrimaryLocale(Market.IN, out _));
        Assert.Throws<MarketPrimaryLocaleUndecidedException>(
            () => MarketPrimaryLocales.Default.GetPrimaryLocale(Market.IN));

        var formatter = new LocalizedMessageFormatter(Registry, MarketPrimaryLocales.Default);
        Assert.Throws<MarketPrimaryLocaleUndecidedException>(
            () => formatter.Format(OrderEmailResourceKeys.ConfirmationSubject, Locale.Parse("fr-FR"), Market.IN, Reference));
    }

    [Theory]
    [Requirement("CC-I18N-006")]
    [InlineData("en-IN")]
    [InlineData("hi-IN")]
    public void The_india_primary_language_is_explicit_configuration(string tag)
    {
        var primaries = MarketPrimaryLocales.WithIndiaPrimary(Locale.Parse(tag));

        Assert.Equal(Locale.Parse(tag), primaries.GetPrimaryLocale(Market.IN));
    }

    [Fact]
    [Requirement("CC-I18N-006")]
    public void The_india_primary_must_be_an_IN_launch_locale()
    {
        Assert.Throws<ArgumentException>(() => MarketPrimaryLocales.WithIndiaPrimary(Locale.Parse("ja-JP")));
    }

    [Fact]
    [Requirement("CC-I18N-006")]
    public void A_key_unresolvable_in_both_locales_fails_closed_never_a_broken_template()
    {
        var formatter = new LocalizedMessageFormatter(Registry, MarketPrimaryLocales.Default);

        Assert.Throws<MessageResourceMissingException>(
            () => formatter.Format("nonexistent.key", Locale.Parse("en-US"), Market.US, Reference));
    }
}
