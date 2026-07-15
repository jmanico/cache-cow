using CacheCow.Modules.MarketGating.Resolution;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.MarketGating.Tests;

/// <summary>
/// Issue 024: geolocation proposes only, explicit choice persists and wins,
/// market and locale are independent, and nothing outside the closed launch
/// sets ever becomes transacting state.
/// </summary>
public sealed class TransactingResolutionTests
{
    private sealed class StubGeolocation(Market? proposal) : IGeolocationMarketProposer
    {
        public Market? ProposeMarket(string? clientIpAddress) => proposal;
    }

    private sealed class ThrowingGeolocation : IGeolocationMarketProposer
    {
        public Market? ProposeMarket(string? clientIpAddress) =>
            throw new InvalidOperationException("geo provider outage");
    }

    private static readonly PreferenceSubject Subject = new("session-1");

    private static TransactingContextResolver Resolver(
        IGeolocationMarketProposer? geolocation = null,
        IMarketPreferenceStore? store = null) =>
        new(store ?? new InMemoryMarketPreferenceStore(), geolocation ?? new StubGeolocation(null));

    [Fact]
    [Requirement("CC-MKT-002")]
    public void First_time_visitor_gets_geolocated_market_as_overridable_proposal_only()
    {
        var resolver = Resolver(new StubGeolocation(Market.US));

        var resolution = resolver.Resolve(Subject, "203.0.113.7");

        Assert.Equal(Market.US, resolution.Market);
        Assert.True(resolution.MarketWasProposedFromGeolocation);

        // The proposal never blocks selecting any of the six markets.
        foreach (var market in Market.All)
        {
            Assert.True(resolver.TrySelectMarket(new PreferenceSubject($"visitor-{market.Code}"), market.Code, out _));
        }
    }

    [Fact]
    [Requirement("CC-MKT-002")]
    public void Persisted_explicit_choice_survives_sessions_and_beats_geolocation()
    {
        var store = new InMemoryMarketPreferenceStore();
        var firstSession = Resolver(new StubGeolocation(Market.DE), store);
        Assert.True(firstSession.TrySelectMarket(Subject, "DE", out _));

        // Later session, IP now geolocates to the US: DE still wins.
        var laterSession = Resolver(new StubGeolocation(Market.US), store);
        var resolution = laterSession.Resolve(Subject, "198.51.100.1");

        Assert.Equal(Market.DE, resolution.Market);
        Assert.False(resolution.MarketWasProposedFromGeolocation);
    }

    [Fact]
    [Requirement("CC-I18N-001")]
    [Requirement("CC-MKT-002")]
    public void Locale_selection_never_changes_the_market_and_vice_versa()
    {
        var store = new InMemoryMarketPreferenceStore();
        var resolver = Resolver(store: store);

        Assert.True(resolver.TrySelectMarket(Subject, "ES", out _));
        Assert.True(resolver.TrySelectLocale(Subject, "de-DE", out _));

        var resolution = resolver.Resolve(Subject, clientIpAddress: null);
        Assert.Equal(Market.ES, resolution.Market); // market unchanged: shopping ES in German is legal
        Assert.Equal(Locale.Parse("de-DE"), resolution.Locale);

        Assert.True(resolver.TrySelectMarket(Subject, "JP", out _));
        resolution = resolver.Resolve(Subject, clientIpAddress: null);
        Assert.Equal(Locale.Parse("de-DE"), resolution.Locale); // locale unchanged: never inferred from market
    }

    [Theory]
    [Requirement("CC-SEC-012")]
    [InlineData("BR")]      // not a launch market
    [InlineData("us")]      // wrong case — reject, never coerce
    [InlineData("")]
    [InlineData(null)]
    [InlineData("IN'--")]
    public void Market_codes_outside_the_closed_launch_set_are_rejected(string? code)
    {
        var store = new InMemoryMarketPreferenceStore();
        var resolver = Resolver(store: store);

        Assert.False(resolver.TrySelectMarket(Subject, code, out _));
        Assert.Null(resolver.Resolve(Subject, null).Market); // never became transacting state
    }

    [Theory]
    [Requirement("CC-SEC-012")]
    [Requirement("CC-I18N-001")]
    [InlineData("fr-FR")]   // well-formed BCP 47, but not a launch locale
    [InlineData("en")]
    [InlineData("xx-XX")]
    [InlineData("")]
    [InlineData(null)]
    public void Locale_tags_outside_the_seven_launch_locales_are_rejected(string? tag)
    {
        var store = new InMemoryMarketPreferenceStore();
        var resolver = Resolver(store: store);

        Assert.False(resolver.TrySelectLocale(Subject, tag, out _));
        Assert.Null(resolver.Resolve(Subject, null).Locale);
    }

    [Theory]
    [Requirement("CC-I18N-001")]
    [InlineData("en-US")]
    [InlineData("es-ES")]
    [InlineData("es-MX")]
    [InlineData("de-DE")]
    [InlineData("ja-JP")]
    [InlineData("en-IN")]
    [InlineData("hi-IN")]
    public void All_seven_launch_locales_are_selectable(string tag)
    {
        var resolver = Resolver();
        Assert.True(resolver.TrySelectLocale(new PreferenceSubject($"subject-{tag}"), tag, out var locale));
        Assert.Equal(Locale.Parse(tag), locale);
    }

    [Fact]
    [Requirement("CC-SEC-012")]
    public void Geolocation_adapter_returning_an_uninitialized_market_yields_no_proposal()
    {
        var resolver = Resolver(new StubGeolocation(default(Market)));

        var resolution = resolver.Resolve(Subject, "203.0.113.7");

        Assert.Null(resolution.Market);
        Assert.False(resolution.MarketWasProposedFromGeolocation);
    }

    [Fact]
    [Requirement("CC-MKT-002")]
    public void Geolocation_failure_degrades_the_proposal_only_and_never_throws_into_gating()
    {
        var resolver = Resolver(new ThrowingGeolocation());

        var resolution = resolver.Resolve(Subject, "203.0.113.7");

        Assert.Null(resolution.Market);
        Assert.Null(resolution.Context); // unresolved → downstream fails closed (deny + no-store)
    }

    [Fact]
    [Requirement("CC-SEC-012")]
    public void Transacting_context_cannot_be_constructed_outside_the_closed_sets()
    {
        // Fail closed at the type boundary: no unknown market, no non-launch
        // locale, can ever become the gating input.
        Assert.Throws<ArgumentException>(() => new TransactingContext(default, Locale.Parse("en-US")));
        Assert.Throws<ArgumentException>(() => new TransactingContext(Market.US, Locale.Parse("fr-FR")));
    }

    [Fact]
    [Requirement("CC-MKT-002")]
    public void Explicit_choice_of_every_launch_market_resolves_as_non_proposal()
    {
        foreach (var market in Market.All)
        {
            var store = new InMemoryMarketPreferenceStore();
            var resolver = Resolver(new StubGeolocation(Market.US), store);
            var subject = new PreferenceSubject($"subject-{market.Code}");

            Assert.True(resolver.TrySelectMarket(subject, market.Code, out _));
            Assert.True(resolver.TrySelectLocale(subject, "en-US", out _));

            var resolution = resolver.Resolve(subject, "203.0.113.7");
            Assert.Equal(market, resolution.Market);
            Assert.False(resolution.MarketWasProposedFromGeolocation);
            Assert.NotNull(resolution.Context);
        }
    }
}
