using CacheCow.Modules.Fulfillment.Auditing;
using CacheCow.Modules.Fulfillment.Routing;
using CacheCow.Modules.Fulfillment.Serviceability;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.Fulfillment.Tests;

/// <summary>
/// Test-only topology and doubles. The real cold-store topology and
/// serviceable-postal-code sets are open operational data (issues 044/045,
/// Open Questions) — these values exist only to exercise the logic.
/// </summary>
internal static class FulfillmentTestData
{
    internal static readonly ColdStoreId UsEast = ColdStoreId.Parse("cs-us-east");
    internal static readonly ColdStoreId UsWest = ColdStoreId.Parse("cs-us-west");
    internal static readonly ColdStoreId EsMadrid = ColdStoreId.Parse("cs-es-madrid");
    internal static readonly ColdStoreId MxCentral = ColdStoreId.Parse("cs-mx-central");
    internal static readonly ColdStoreId DeCentral = ColdStoreId.Parse("cs-de-central");
    internal static readonly ColdStoreId JpKanto = ColdStoreId.Parse("cs-jp-kanto");
    internal static readonly ColdStoreId InSouth = ColdStoreId.Parse("cs-in-south");

    internal static InMemoryServingRegionSource ServingRegions() => new(
    [
        new ServingRegionEntry(Market.US, PostalCode.Parse("10001"), UsEast),
        new ServingRegionEntry(Market.US, PostalCode.Parse("94103"), UsWest),
        new ServingRegionEntry(Market.ES, PostalCode.Parse("28001"), EsMadrid),
        new ServingRegionEntry(Market.MX, PostalCode.Parse("06000"), MxCentral),
        new ServingRegionEntry(Market.DE, PostalCode.Parse("10115"), DeCentral),
        new ServingRegionEntry(Market.JP, PostalCode.Parse("100-0001"), JpKanto),
        new ServingRegionEntry(Market.IN, PostalCode.Parse("560001"), InSouth),
    ]);

    internal static InMemoryPostalCodeServiceabilitySource ServiceablePostalCodes() => new(
    [
        new ServiceablePostalCode(Market.US, PostalCode.Parse("10001")),
        new ServiceablePostalCode(Market.US, PostalCode.Parse("94103")),
        new ServiceablePostalCode(Market.ES, PostalCode.Parse("28001")),
        new ServiceablePostalCode(Market.MX, PostalCode.Parse("06000")),
        new ServiceablePostalCode(Market.DE, PostalCode.Parse("10115")),
        new ServiceablePostalCode(Market.JP, PostalCode.Parse("100-0001")),
        new ServiceablePostalCode(Market.IN, PostalCode.Parse("560001")),
        // Serviceable per the postal set, but no serving store exists — the
        // routing precondition must still deny (fail closed).
        new ServiceablePostalCode(Market.US, PostalCode.Parse("73301")),
    ]);
}

internal sealed class RecordingAuditSink : IFulfillmentAuditSink
{
    internal List<FulfillmentAuditEvent> Events { get; } = [];

    internal bool AppendSucceeds { get; set; } = true;

    internal bool ThrowOnAppend { get; set; }

    public bool TryAppend(FulfillmentAuditEvent auditEvent)
    {
        if (ThrowOnAppend)
        {
            throw new InvalidOperationException("Audit store unavailable.");
        }

        if (!AppendSucceeds)
        {
            return false;
        }

        Events.Add(auditEvent);
        return true;
    }
}

internal sealed class StubTransitTimeEstimator : ITransitTimeEstimator
{
    internal TimeSpan? Result { get; set; }

    internal bool ThrowOnEstimate { get; set; }

    internal ColdStoreId? LastOrigin { get; private set; }

    public TimeSpan? EstimateTransit(ColdStoreId originStore, Market market, PostalCode destinationPostalCode)
    {
        if (ThrowOnEstimate)
        {
            throw new InvalidOperationException("Carrier aggregator unavailable.");
        }

        LastOrigin = originStore;
        return Result;
    }
}

internal sealed class ThrowingServingRegionSource : IServingRegionSource
{
    public bool ServesMarket(Market market) => throw new InvalidOperationException("Serving-region data unavailable.");

    public ColdStoreId? FindServingStore(Market market, PostalCode postalCode) =>
        throw new InvalidOperationException("Serving-region data unavailable.");

    public bool IsKnownStore(ColdStoreId store) => throw new InvalidOperationException("Serving-region data unavailable.");
}

internal sealed class ThrowingPostalCodeServiceabilitySource : IPostalCodeServiceabilitySource
{
    public bool IsServiceable(Market market, PostalCode postalCode) =>
        throw new InvalidOperationException("Serviceability data unavailable.");
}

internal sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _utcNow;

    internal FixedTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;
}
