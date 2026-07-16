using System.Reflection;
using CacheCow.Modules.BackOffice.Orders;
using CacheCow.Modules.BackOffice.Partners;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.BackOffice.Tests;

/// <summary>
/// Structural guards on the dashboard read models (issue 082, AC-01:
/// "role-shaped fields only"; Data Classification: orders are Restricted/PII).
///
/// These are reflection tests on purpose. A behavioral test only proves the
/// fields present today behave; it cannot notice a <c>CustomerEmail</c> added
/// next quarter. Pinning the EXACT public shape means widening a row is a
/// deliberate act that fails a test naming the requirement, rather than a
/// quiet PII leak into every search grid, log line, and audit summary.
/// </summary>
public sealed class DashboardRowShapeTests
{
    private static IReadOnlyList<PropertyInfo> PublicProperties<T>() =>
        [.. typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            // Records synthesize EqualityContract; it is not a data field.
            .Where(property => !string.Equals(property.Name, "EqualityContract", StringComparison.Ordinal))
            .OrderBy(property => property.Name, StringComparer.Ordinal)];

    [Fact]
    [Requirement("CC-DSH-003")]
    public void OrderRow_ExposesExactlyThePiiMinimalOperationalFields()
    {
        var names = PublicProperties<DashboardOrderRow>().Select(property => property.Name).ToList();

        // Order reference, market, state, placement time, total — and nothing
        // else. No customer name, email, address, phone, or payment detail:
        // the search grid needs none of it (issue 082, AC-01).
        Assert.Equal(
            ["Market", "OrderRef", "PlacedAt", "State", "Total"],
            names);
    }

    [Fact]
    [Requirement("CC-DSH-003")]
    public void OrderRow_CarriesNoFieldNamedLikeCustomerPii()
    {
        // A second, name-based net: it catches a PII field added under a type
        // the exact-shape test above would also catch, but reports it in the
        // vocabulary a reviewer recognizes.
        string[] forbidden = ["customer", "email", "address", "phone", "name", "card", "pan", "payment"];

        foreach (var property in PublicProperties<DashboardOrderRow>())
        {
            foreach (var fragment in forbidden)
            {
                Assert.False(
                    property.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase),
                    $"DashboardOrderRow.{property.Name} looks like customer PII; order rows are PII-minimal (issue 082, AC-01).");
            }
        }
    }

    [Fact]
    [Requirement("CC-WHS-002")]
    public void PartnerRow_ExposesNoPartnerContactPii()
    {
        // Partner CONTACT details are Restricted/PII (issue 085, Data
        // Classification); the business name is the company's, not a person's.
        var names = PublicProperties<DashboardPartnerRow>().Select(property => property.Name).ToList();

        Assert.Equal(["LegalName", "PartnerId", "State"], names);
    }

    [Fact]
    [Requirement("CC-PRC-003")]
    public void OrderRow_HoldsMoneyOnlyAsTheSharedIntegerMinorUnitType()
    {
        var total = typeof(DashboardOrderRow).GetProperty(nameof(DashboardOrderRow.Total))!;

        Assert.Equal(typeof(Money), total.PropertyType);
    }

    /// <summary>
    /// CC-PRC-003 bans binary floating point for money "anywhere, including
    /// tests". This sweeps every public type in the module rather than the
    /// money-carrying ones only: the ban is easiest to violate somewhere
    /// nobody thought of as a money surface — a rate, a metric, a total on a
    /// report — which is exactly why the CC-DSH-006 service level is integer
    /// basis points.
    /// </summary>
    [Fact]
    [Requirement("CC-PRC-003")]
    public void NoPublicTypeInTheModuleExposesFloatingPoint()
    {
        var floatTypes = new[] { typeof(double), typeof(float), typeof(double?), typeof(float?) };
        var offenders = new List<string>();

        foreach (var type in typeof(DashboardOrderRow).Assembly.GetExportedTypes())
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (floatTypes.Contains(property.PropertyType))
                {
                    offenders.Add($"{type.Name}.{property.Name} ({property.PropertyType.Name})");
                }
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                if (floatTypes.Contains(method.ReturnType))
                {
                    offenders.Add($"{type.Name}.{method.Name}() -> {method.ReturnType.Name}");
                }

                foreach (var parameter in method.GetParameters())
                {
                    if (floatTypes.Contains(parameter.ParameterType))
                    {
                        offenders.Add($"{type.Name}.{method.Name}({parameter.Name}: {parameter.ParameterType.Name})");
                    }
                }
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    [Requirement("CC-ORD-006")]
    public void OrderStates_AreExactlyTheCcOrd006ClosedSet()
    {
        var names = DashboardOrderStates.All.Select(DashboardOrderStates.NameOf).ToList();

        // received -> confirmed -> packed -> shipped -> delivered, plus the
        // cancelled and refunded terminal branches.
        Assert.Equal(
            ["received", "confirmed", "packed", "shipped", "delivered", "cancelled", "refunded"],
            names);
    }

    [Fact]
    [Requirement("CC-ORD-006")]
    public void OrderStateParsing_IsExactAndFailsClosed()
    {
        Assert.True(DashboardOrderStates.TryParse("delivered", out var state));
        Assert.Equal(DashboardOrderState.Delivered, state);

        // Case-shifted, padded, unknown, and empty names do not resolve.
        Assert.False(DashboardOrderStates.TryParse("Delivered", out _));
        Assert.False(DashboardOrderStates.TryParse(" delivered ", out _));
        Assert.False(DashboardOrderStates.TryParse("teleported", out _));
        Assert.False(DashboardOrderStates.TryParse(null, out _));
    }

    /// <summary>
    /// The refund port takes no amount: partial refunds are unspecified, and
    /// accepting an amount would silently invent them while also accepting a
    /// client-supplied monetary value (CC-PRC-005; issue 082, Anti-Patterns
    /// and Open Questions).
    /// </summary>
    [Fact]
    [Requirement("CC-PRC-005")]
    public void RefundPort_AcceptsNoCallerSuppliedAmount()
    {
        var refund = typeof(IDashboardOrderCommands).GetMethod(nameof(IDashboardOrderCommands.Refund))!;

        Assert.DoesNotContain(refund.GetParameters(), parameter => parameter.ParameterType == typeof(Money));
    }
}
