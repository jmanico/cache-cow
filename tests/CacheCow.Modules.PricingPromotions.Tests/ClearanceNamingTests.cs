using System.Globalization;
using System.Reflection;
using CacheCow.Modules.PricingPromotions.Promotions;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.PricingPromotions.Tests;

/// <summary>
/// CC-PRC-007: "Eviction Specials" is presentation-only naming (DESIGN.md §5.3)
/// and must never leak into pricing or invoice-facing data. The engine
/// vocabulary is neutral — a boolean clearance classification at most.
/// </summary>
public sealed class ClearanceNamingTests
{
    [Fact]
    [Requirement("CC-PRC-007")]
    public void No_public_engine_vocabulary_carries_the_presentation_branding()
    {
        var assembly = typeof(Promotion).Assembly;
        var offenders = new List<string>();

        foreach (var type in assembly.GetExportedTypes())
        {
            if (type.Name.Contains("Eviction", StringComparison.OrdinalIgnoreCase))
            {
                offenders.Add(type.FullName ?? type.Name);
            }

            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                if (member.Name.Contains("Eviction", StringComparison.OrdinalIgnoreCase))
                {
                    offenders.Add($"{type.Name}.{member.Name}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Presentation branding leaked into engine vocabulary (CC-PRC-007): " + string.Join(", ", offenders));
    }

    [Fact]
    [Requirement("CC-PRC-007")]
    public void A_clearance_promotion_exposes_only_a_neutral_flag_and_neutral_identifier()
    {
        var clearance = new Promotion(
            "clearance-2026-07", Market.US,
            new PercentageDiscount(3_000),
            PromotionScope.ForSku(SkuId.Parse("SKU-BRISKET-01")),
            DateTime.Parse("2026-07-01T00:00:00", CultureInfo.InvariantCulture),
            DateTime.Parse("2026-08-01T00:00:00", CultureInfo.InvariantCulture),
            isClearance: true);

        Assert.True(clearance.IsClearance);

        // Everything invoice generation could read from this record is neutral:
        // no property value carries the DESIGN.md §5.3 branding.
        foreach (var property in typeof(Promotion).GetProperties())
        {
            var value = property.GetValue(clearance)?.ToString() ?? string.Empty;
            Assert.DoesNotContain("Eviction", value, StringComparison.OrdinalIgnoreCase);
        }
    }
}
