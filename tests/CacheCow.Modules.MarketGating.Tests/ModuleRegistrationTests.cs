using CacheCow.Modules.MarketGating;
using CacheCow.Modules.MarketGating.Enforcement;
using CacheCow.Modules.MarketGating.Resolution;
using CacheCow.SharedKernel.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CacheCow.Modules.MarketGating.Tests;

/// <summary>
/// The module registers its enforcement point, resolver, and ports; the host
/// can replace the provisional port defaults (TryAdd) once the open decisions
/// land (ARCHITECTURE.md, "Known unknowns").
/// </summary>
public sealed class ModuleRegistrationTests
{
    [Fact]
    [Requirement("CC-MKT-006")]
    public void AddMarketGatingModule_resolves_the_enforcement_point_and_resolver()
    {
        using var provider = new ServiceCollection()
            .AddMarketGatingModule()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.IsType<MarketGatingService>(provider.GetRequiredService<IMarketGatingService>());
        Assert.NotNull(provider.GetRequiredService<TransactingContextResolver>());
        Assert.IsType<InMemoryMarketPreferenceStore>(provider.GetRequiredService<IMarketPreferenceStore>());
        Assert.IsType<NullGeolocationMarketProposer>(provider.GetRequiredService<IGeolocationMarketProposer>());
    }

    [Fact]
    public void Host_supplied_port_implementations_win_over_the_provisional_defaults()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGeolocationMarketProposer, FakeProposer>();
        services.AddMarketGatingModule();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<FakeProposer>(provider.GetRequiredService<IGeolocationMarketProposer>());
    }

    private sealed class FakeProposer : IGeolocationMarketProposer
    {
        public CacheCow.SharedKernel.Market? ProposeMarket(string? clientIpAddress) => null;
    }
}
