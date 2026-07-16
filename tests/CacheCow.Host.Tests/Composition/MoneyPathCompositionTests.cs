using CacheCow.Modules.CatalogInventory.Catalog;
using CacheCow.Modules.OrderingPayments.Submission;
using CacheCow.Modules.PricingPromotions.Pricing;
using CacheCow.Modules.PricingPromotions.Promotions;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using HostComposition = CacheCow.Host.Composition;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CacheCow.Host.Tests.Composition;

/// <summary>
/// Host composition of the consumer money path: order submission recomputes
/// every amount from the PricingPromotions PriceList through the host
/// adapters (CC-PRC-005; ARCHITECTURE.md, Dependency rule 2 — money flows one
/// way), promotion evaluation runs the real engine with an EXPLICIT,
/// clearly-marked TEST-ONLY rounding-mode configuration (the policy itself is
/// unratified), and the unconfigured state fails closed instead of defaulting
/// (CC-PRC-003).
/// </summary>
public sealed class MoneyPathCompositionTests
{
    private const string Sku = "RIBS-01";
    private const long UnitPriceCents = 5_000;

    /// <summary>TEST-ONLY zero tax so the suite exercises the pricing seam, not the (unwired) external tax port.</summary>
    private sealed class ZeroTaxCalculatorTestDouble : ITaxCalculator
    {
        public Money CalculateTax(Market market, Money taxableTotal) =>
            Money.FromMinorUnits(0, taxableTotal.Currency);
    }

    private sealed class SinglePromotionSource : HostComposition.IActivePromotionSource
    {
        private readonly Promotion _promotion;

        public SinglePromotionSource(Promotion promotion)
        {
            _promotion = promotion;
        }

        public IReadOnlyList<Promotion> CandidatesFor(Market market) =>
            _promotion.Market == market ? [_promotion] : [];
    }

    private static Promotion TenPercentOff(Market market) => new(
        "promo-10",
        market,
        new PercentageDiscount(1_000),
        PromotionScope.ForSku(SkuId.Parse(Sku)),
        new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Unspecified),
        new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Unspecified));

    private static WebApplicationFactory<Program> CreateHost(
        bool withRoundingConfig,
        Promotion? promotion = null)
    {
        var config = new Dictionary<string, string?>();
        if (withRoundingConfig)
        {
            // TEST-ONLY rounding mode: the production policy is unratified
            // (issues 002/033/034) and the shipped configuration carries no
            // value — this explicit setting exists solely so the suite can
            // drive the composed path end to end.
            config[HostComposition.MoneyRoundingPolicy.ConfigurationKey] = "HalfToEven";
        }

        var factory = TestHostBuilder.Create(config, configureServices: services =>
        {
            services.AddSingleton(new PriceList(
            [
                new MarketPrice(SkuId.Parse(Sku), Market.US, Money.FromMinorUnits(UnitPriceCents, Currency.Usd)),
                new MarketPrice(SkuId.Parse(Sku), Market.IN, Money.FromMinorUnits(UnitPriceCents, Currency.Inr)),
            ]));

            // TEST-ONLY: max quantity per line is unratified required config.
            services.AddSingleton(new OrderSubmissionOptions(maxQuantityPerLine: 100));
            services.AddSingleton<ITaxCalculator, ZeroTaxCalculatorTestDouble>();

            if (promotion is not null)
            {
                services.AddSingleton<HostComposition.IActivePromotionSource>(new SinglePromotionSource(promotion));

                // TEST-ONLY timezone mapping (the market-to-zone mapping is
                // an open decision; the shipped default fails closed).
                services.AddSingleton<IMarketTimeZoneProvider>(new MarketTimeZoneMap(
                    new Dictionary<Market, string> { [promotion.Market] = "America/Chicago" }));
            }
        });

        B2BFixtures.SeedCatalog(
            factory,
            B2BFixtures.CatalogSku(Sku, ProductClassification.NonVegetarian, Market.US, Market.DE));

        return factory;
    }

    private static OrderSubmissionRequest ThreeUnits() =>
        new([new SubmittedCartLine(SkuId.Parse(Sku), 3)]);

    [Fact]
    [Requirement("CC-PRC-005")]
    public void Submission_recomputes_all_money_from_the_canonical_price_list()
    {
        using var factory = CreateHost(withRoundingConfig: true);
        var submission = factory.Services.GetRequiredService<OrderSubmissionService>();

        var order = submission.Submit(ThreeUnits(), BuyerIdentity.ForGuestSession("guest-1"), Market.US);

        // 3 x 5000 cents, from the PriceList — the request carries no price
        // field at all (unrepresentable, CC-PRC-005).
        Assert.Equal(15_000, order.Subtotal.MinorUnits);
        Assert.Equal(0, order.DiscountTotal.MinorUnits);
        Assert.Equal(15_000, order.GrandTotal.MinorUnits);
        Assert.Equal(Currency.Usd, order.GrandTotal.Currency);
        Assert.Equal(UnitPriceCents, order.Lines[0].UnitPrice.MinorUnits);
    }

    [Fact]
    [Requirement("CC-PRC-005")]
    [Requirement("CC-PRC-006")]
    public void Promotion_applies_through_the_real_engine_with_explicit_test_only_rounding()
    {
        using var factory = CreateHost(withRoundingConfig: true, promotion: TenPercentOff(Market.US));
        var submission = factory.Services.GetRequiredService<OrderSubmissionService>();

        var order = submission.Submit(ThreeUnits(), BuyerIdentity.ForGuestSession("guest-2"), Market.US);

        // 10% of 15000 = 1500 cents off, computed by the PricingPromotions
        // engine under the explicit test rounding mode.
        Assert.Equal(15_000, order.Subtotal.MinorUnits);
        Assert.Equal(1_500, order.DiscountTotal.MinorUnits);
        Assert.Equal(13_500, order.GrandTotal.MinorUnits);
    }

    [Fact]
    [Requirement("CC-PRC-003")]
    public void Unconfigured_rounding_policy_fails_the_submission_closed_when_a_promotion_is_in_play()
    {
        using var factory = CreateHost(withRoundingConfig: false, promotion: TenPercentOff(Market.US));
        var submission = factory.Services.GetRequiredService<OrderSubmissionService>();

        // The rounding policy is unratified: with candidate promotions in
        // play the adapter surfaces the unconfigured state as an error and no
        // order is created — never a silently defaulted rounding mode.
        var failure = Assert.Throws<InvalidOperationException>(() =>
            submission.Submit(ThreeUnits(), BuyerIdentity.ForGuestSession("guest-3"), Market.US));
        Assert.Contains("AWAITING HUMAN RATIFICATION", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-MKT-003")]
    [Requirement("CC-PRC-005")]
    public void Consumer_submission_of_a_nonveg_sku_in_IN_is_rejected_by_the_real_gating_service()
    {
        using var factory = CreateHost(withRoundingConfig: true);
        var submission = factory.Services.GetRequiredService<OrderSubmissionService>();

        // The price list HAS an IN row for the SKU: only market gating —
        // consulted by the host price-source adapter through the real
        // MarketGating service — stops the sale (storefront/B2B parity).
        var rejection = Assert.Throws<OrderSubmissionRejectedException>(() =>
            submission.Submit(ThreeUnits(), BuyerIdentity.ForGuestSession("guest-4"), Market.IN));
        Assert.Equal(OrderSubmissionRejection.SkuUnavailableInTransactingMarket, rejection.Reason);
    }
}
