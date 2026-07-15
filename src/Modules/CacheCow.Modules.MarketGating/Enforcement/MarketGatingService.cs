using CacheCow.Modules.MarketGating.Policy;
using CacheCow.Modules.MarketGating.Resolution;

namespace CacheCow.Modules.MarketGating.Enforcement;

/// <summary>
/// Default implementation of the enforcement point. Pure function of the
/// server-side transacting context and the declarative policy table
/// (<see cref="LaunchMarketPolicies"/>): no client hint can influence an
/// outcome because none is accepted as input, by construction (CC-SEC-012).
/// Every failure path is a denial (SECURITY.md, Logging rule 2).
/// </summary>
public sealed class MarketGatingService : IMarketGatingService
{
    public GatingDecision EvaluateSku(TransactingContext context, SkuGatingSubject subject, ResponseSurface surface)
    {
        if (context is null || subject is null)
        {
            return GatingDecision.ExcludedAsNotFound(GatingDenialReason.MissingGatingInput);
        }

        if (!Enum.IsDefined(surface))
        {
            return GatingDecision.ExcludedAsNotFound(GatingDenialReason.UnknownResponseSurface);
        }

        if (!LaunchMarketPolicies.TryGet(context.Market, out var policy))
        {
            return GatingDecision.ExcludedAsNotFound(GatingDenialReason.UnknownMarketPolicy);
        }

        // Veg SKUs are available in all markets (CC-MKT-007); non-veg SKUs are
        // allowed only where policy permits them — excluded from every surface
        // of a veg-only market (CC-MKT-003). Anything else is unclassifiable
        // and denies (fail closed).
        return subject.Classification switch
        {
            SkuClassification.Vegetarian => GatingDecision.Allowed,
            SkuClassification.NonVegetarian when policy.NonVegSkusPermitted => GatingDecision.Allowed,
            SkuClassification.NonVegetarian => GatingDecision.ExcludedAsNotFound(GatingDenialReason.NonVegExcludedFromMarket),
            _ => GatingDecision.ExcludedAsNotFound(GatingDenialReason.UnknownSkuClassification),
        };
    }

    public GatingDecision EvaluateContentExperience(TransactingContext context, ContentExperience experience)
    {
        if (context is null)
        {
            return GatingDecision.ExcludedAsNotFound(GatingDenialReason.MissingGatingInput);
        }

        if (!Enum.IsDefined(experience))
        {
            return GatingDecision.ExcludedAsNotFound(GatingDenialReason.UnknownContentExperience);
        }

        if (!LaunchMarketPolicies.TryGet(context.Market, out var policy))
        {
            return GatingDecision.ExcludedAsNotFound(GatingDenialReason.UnknownMarketPolicy);
        }

        return policy.PlacementOf(experience) == ContentPlacement.NotAvailable
            ? GatingDecision.ExcludedAsNotFound(GatingDenialReason.ContentExperienceUnavailableInMarket)
            : GatingDecision.Allowed;
    }

    public ContentPlacement PlacementOf(TransactingContext context, ContentExperience experience)
    {
        if (context is null
            || !Enum.IsDefined(experience)
            || !LaunchMarketPolicies.TryGet(context.Market, out var policy))
        {
            return ContentPlacement.NotAvailable; // fail closed
        }

        return policy.PlacementOf(experience);
    }

    public Gated<IReadOnlyList<T>> FilterSkus<T>(
        TransactingContext context,
        IEnumerable<T> items,
        Func<T, SkuClassification> classification,
        ResponseSurface surface)
    {
        // The bulk path must fail closed, not open: with any invalid input the
        // gated result is empty — never the unfiltered set (SECURITY.md,
        // Logging rule 2). A null context cannot even produce a Gated<T>, so
        // it throws rather than fabricating a context.
        ArgumentNullException.ThrowIfNull(context);

        if (items is null || classification is null || !Enum.IsDefined(surface))
        {
            return new Gated<IReadOnlyList<T>>(context, []);
        }

        var permitted = new List<T>();
        foreach (var item in items)
        {
            if (TryClassify(classification, item, out var itemClass)
                && EvaluateSku(context, new SkuGatingSubject(PlaceholderSkuId, itemClass), surface).IsAllowed)
            {
                permitted.Add(item);
            }
        }

        return new Gated<IReadOnlyList<T>>(context, permitted);
    }

    // FilterSkus gates on classification; identity is not needed for the
    // decision, but EvaluateSku's subject shape requires one.
    private static readonly SharedKernel.SkuId PlaceholderSkuId = SharedKernel.SkuId.Parse("gating-filter");

    private static bool TryClassify<T>(Func<T, SkuClassification> classification, T item, out SkuClassification result)
    {
        try
        {
            result = classification(item);
            return true;
        }
#pragma warning disable CA1031 // Fail closed: any exception in a gating path is a denial (exclusion), never a bypass (SECURITY.md, Logging rule 2).
        catch (Exception)
#pragma warning restore CA1031
        {
            result = default; // NonVegetarian — most restrictive
            return false;
        }
    }
}
