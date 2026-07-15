using CacheCow.Modules.PricingPromotions.Promotions;
using CacheCow.Modules.PricingPromotions.Rounding;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CacheCow.Modules.PricingPromotions.Tests;

/// <summary>
/// Module wiring: the default market-timezone port is a fail-closed
/// placeholder (the canonical mapping is an open decision, issue 033 Open
/// Questions) that the host must replace with configuration; nothing in this
/// module guesses a zone.
/// </summary>
public sealed class ModuleRegistrationTests
{
    private static readonly SkuId Brisket = SkuId.Parse("SKU-BRISKET-01");

    private static PromotionEvaluationRequest RequestWithOnePromotion()
    {
        var promotion = new Promotion(
            "promo", Market.JP, new PercentageDiscount(1_000), PromotionScope.ForSku(Brisket),
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Unspecified),
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Unspecified));
        var line = new PromotionLine(Brisket, Money.FromMinorUnits(14_900, Currency.Jpy), 1);
        return new PromotionEvaluationRequest(Market.JP, [line], [promotion], RoundingMode.HalfToEven);
    }

    [Fact]
    [Requirement("CC-PRC-006")]
    public void Evaluator_resolves_and_uses_the_host_supplied_timezone_configuration()
    {
        var services = new ServiceCollection().AddPricingPromotionsModule();
        services.AddSingleton<IMarketTimeZoneProvider>(
            new MarketTimeZoneMap(new Dictionary<Market, string> { [Market.JP] = "Asia/Tokyo" }));
        services.AddSingleton<TimeProvider>(
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero)));

        using var provider = services.BuildServiceProvider();
        var evaluator = provider.GetRequiredService<IPromotionEvaluator>();

        var result = evaluator.Evaluate(RequestWithOnePromotion());

        Assert.Equal("promo", result.Lines[0].AppliedPromotionId);
    }

    [Fact]
    [Requirement("CC-PRC-006")]
    public void The_default_timezone_port_fails_closed_until_the_open_decision_is_ratified()
    {
        // No default market-to-timezone mapping exists (issue 033, Open
        // Questions): with only the module's provisional registration, window
        // evaluation is a denial, never a guessed zone or a granted discount.
        using var provider = new ServiceCollection()
            .AddPricingPromotionsModule()
            .BuildServiceProvider();

        Assert.IsType<UnconfiguredMarketTimeZoneProvider>(
            provider.GetRequiredService<IMarketTimeZoneProvider>());

        var evaluator = provider.GetRequiredService<IPromotionEvaluator>();
        Assert.Throws<MarketTimeZoneUnavailableException>(() => evaluator.Evaluate(RequestWithOnePromotion()));
    }
}
