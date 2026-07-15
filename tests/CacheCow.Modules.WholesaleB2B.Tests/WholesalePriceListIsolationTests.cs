using System.Reflection;
using CacheCow.Modules.WholesaleB2B.Partners;
using CacheCow.Modules.WholesaleB2B.PriceLists;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.WholesaleB2B.Tests;

/// <summary>
/// Issue 050 (CC-WHS-003): wholesale prices and terms are reachable only
/// through a tenant-scoped <see cref="PartnerTenantContext"/> — consumer,
/// guest, and anonymous access is unrepresentable at the type level, every
/// lookup is scoped to the context's own tenant, cross-tenant reads fail
/// closed with an existence-hiding denial (CC-QA-005; SECURITY.md,
/// Authentication rules 8–9), and payment terms resolve to the ratified
/// net-60 default unless a per-partner adjustment exists (CC-WHS-004).
/// </summary>
public sealed class WholesalePriceListIsolationTests
{
    private static readonly SkuId Brisket = SkuId.Parse("SKU-BRISKET");
    private static readonly SkuId Ribs = SkuId.Parse("SKU-RIBS");

    private static WholesalePriceList DeListFor(string partnerId, long caseMinorUnits) =>
        new(
            PartnerId.Parse(partnerId),
            Market.DE,
            [new WholesalePriceLine(Brisket, 12, Money.FromMinorUnits(caseMinorUnits, Currency.Eur))]);

    /// <summary>Partner A (DE only) and partner B (DE and JP), both with registered DE lists, B also with a JP list.</summary>
    private static (InMemoryWholesalePriceLists Store, PartnerTenantContext A, PartnerTenantContext B) SeedTwoPartners()
    {
        var store = new InMemoryWholesalePriceLists();

        var contextA = Fixtures.ApprovedContext("partner-a", Fixtures.DeIdentity());
        var contextB = Fixtures.ApprovedContext("partner-b", Fixtures.DeIdentity(), Fixtures.JpIdentity());

        store.Register(DeListFor("partner-a", 29_988));
        store.Register(DeListFor("partner-b", 24_988));
        store.Register(new WholesalePriceList(
            PartnerId.Parse("partner-b"),
            Market.JP,
            [new WholesalePriceLine(Ribs, 24, Money.FromMinorUnits(90_000, Currency.Jpy))]));

        return (store, contextA, contextB);
    }

    [Fact]
    [Requirement("CC-WHS-003")]
    public void Each_context_receives_only_its_own_partners_list()
    {
        var (store, contextA, contextB) = SeedTwoPartners();

        var listA = store.GetPriceList(contextA, Market.DE);
        var listB = store.GetPriceList(contextB, Market.DE);

        Assert.Equal(contextA.PartnerId, listA.Owner);
        Assert.Equal(contextB.PartnerId, listB.Owner);
        Assert.NotEqual(listA.Owner, listB.Owner);

        Assert.True(listA.TryGetLine(Brisket, out var lineA));
        Assert.Equal(Money.FromMinorUnits(29_988, Currency.Eur), lineA!.PricePerCase);
    }

    [Fact]
    [Requirement("CC-WHS-003")]
    [Requirement("CC-QA-005")]
    public void Cross_tenant_reads_fail_closed_without_confirming_existence()
    {
        var (store, contextA, _) = SeedTwoPartners();

        // The API gives partner A no way to name partner B: the only variable
        // input is the market. Partner B has a JP list; A requesting JP must
        // be denied even though the resource exists...
        var crossTenant = Assert.Throws<WholesalePriceListUnavailableException>(
            () => store.GetPriceList(contextA, Market.JP));

        // ...and the denial must be indistinguishable from "no such list":
        // partner C is authorized for US where nothing is registered.
        var contextC = Fixtures.ApprovedContext("partner-c", Fixtures.UsIdentity());
        var missing = Assert.Throws<WholesalePriceListUnavailableException>(
            () => store.GetPriceList(contextC, Market.US));

        Assert.Equal(missing.Message, crossTenant.Message);
    }

    [Fact]
    [Requirement("CC-WHS-003")]
    [Requirement("CC-QA-005")]
    public void A_market_outside_the_tenants_authorization_is_denied_even_for_its_own_data()
    {
        var store = new InMemoryWholesalePriceLists();

        // A DE list registered for partner A while A's tenancy only captured
        // US identity: server-side authorization state wins (CC-API-007 parity).
        store.Register(DeListFor("partner-a", 29_988));
        var usOnlyContext = Fixtures.ApprovedContext("partner-a", Fixtures.UsIdentity());

        Assert.Throws<WholesalePriceListUnavailableException>(
            () => store.GetPriceList(usOnlyContext, Market.DE));
    }

    [Fact]
    [Requirement("CC-WHS-004")]
    public void Payment_terms_default_to_net_60_and_per_partner_overrides_stay_per_partner()
    {
        var (store, contextA, contextB) = SeedTwoPartners();

        // Ratified 2026-07-15 default: net-60 with no adjustment on file.
        Assert.Equal(PaymentTerms.Net60Default, store.GetPaymentTerms(contextA));
        Assert.Equal(60, store.GetPaymentTerms(contextA).NetDays);

        // A per-partner adjustment overrides the default for that partner only.
        store.SetPaymentTerms(contextA.PartnerId, PaymentTerms.Net(30));

        Assert.Equal(30, store.GetPaymentTerms(contextA).NetDays);
        Assert.Equal(60, store.GetPaymentTerms(contextB).NetDays);
    }

    [Fact]
    [Requirement("CC-WHS-003")]
    [Requirement("CC-QA-005")]
    public void No_public_read_path_accepts_anything_but_a_tenant_context()
    {
        // Type-level enforcement of "invisible to consumer sessions and not
        // derivable from consumer API responses": every public member of the
        // read port takes a PartnerTenantContext, and across the whole module
        // assembly the only public methods returning wholesale price or terms
        // data — beyond the creation APIs declared on the data types
        // themselves — require a PartnerTenantContext parameter. There is no
        // overload keyed by raw PartnerId, order number, session string, or
        // any other caller-suppliable substitute.
        foreach (var method in typeof(IWholesalePriceLists).GetMethods())
        {
            Assert.Contains(method.GetParameters(), p => p.ParameterType == typeof(PartnerTenantContext));
        }

        Type[] wholesaleDataTypes = [typeof(WholesalePriceList), typeof(WholesalePriceLine), typeof(PaymentTerms)];

        var retrievalMethods = typeof(WholesaleB2BModule).Assembly
            .GetExportedTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Where(m => wholesaleDataTypes.Contains(m.ReturnType)
                || m.GetParameters().Any(p => p.IsOut && wholesaleDataTypes.Contains(p.ParameterType.GetElementType())))
            .Where(m => !wholesaleDataTypes.Contains(m.DeclaringType)) // creation/self APIs on the data types themselves
            .ToArray();

        Assert.NotEmpty(retrievalMethods);
        foreach (var method in retrievalMethods)
        {
            Assert.True(
                method.GetParameters().Any(p => p.ParameterType == typeof(PartnerTenantContext)),
                $"{method.DeclaringType!.Name}.{method.Name} exposes wholesale data without requiring a PartnerTenantContext (CC-WHS-003).");
        }
    }

    [Fact]
    [Requirement("CC-WHS-001")]
    public void Registration_is_validated_and_refuses_silent_overwrites()
    {
        var store = new InMemoryWholesalePriceLists();
        store.Register(DeListFor("partner-a", 29_988));

        // Update semantics await the price-list-administration open decision
        // (issue 050, Open Questions): re-registration fails closed.
        Assert.Throws<WholesaleValidationException>(() => store.Register(DeListFor("partner-a", 19_988)));

        Assert.Throws<WholesaleValidationException>(
            () => store.SetPaymentTerms(default, PaymentTerms.Net(30)));
        Assert.Throws<WholesaleValidationException>(
            () => store.SetPaymentTerms(PartnerId.Parse("partner-a"), default));
    }
}
