using CacheCow.Modules.Fulfillment.Auditing;
using CacheCow.Modules.Fulfillment.Routing;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.Fulfillment.Tests;

/// <summary>
/// Issue 044 (CC-FUL-001, CC-DSH-004): cross-region fulfillment exists only
/// through an explicit, audited operations override. The audit event is
/// appended before the override takes effect; a failed append denies it, so an
/// unaudited cross-region fulfillment cannot exist (AC-03/AC-07).
/// </summary>
public sealed class CrossRegionOverrideTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly OrderReference Order = OrderReference.Parse("order-2002");
    private static readonly OverrideAuthorization OpsPermission = OverrideAuthorization.IssuedTo("staff-ops-7");

    private static ColdStoreAssignment RoutedToUsEast() =>
        new(Order, FulfillmentTestData.UsEast, FulfillmentTestData.UsEast);

    private static OrderRoutingService CreateService(RecordingAuditSink sink) =>
        new(FulfillmentTestData.ServingRegions(), sink, new FixedTimeProvider(FixedNow));

    [Fact]
    [Requirement("CC-FUL-001")]
    [Requirement("CC-DSH-004")]
    public void Audited_override_reroutes_and_the_audit_event_carries_actor_order_and_both_stores()
    {
        var sink = new RecordingAuditSink();
        var service = CreateService(sink);

        var result = service.ApplyCrossRegionOverride(RoutedToUsEast(), FulfillmentTestData.UsWest, OpsPermission);

        Assert.True(result.IsApplied);
        Assert.Equal(OverrideDenialReason.None, result.Denial);
        Assert.NotNull(result.Assignment);
        Assert.Equal(FulfillmentTestData.UsWest, result.Assignment.AssignedStore);
        Assert.Equal(FulfillmentTestData.UsEast, result.Assignment.ServingStore);
        Assert.True(result.Assignment.IsCrossRegion);

        var auditEvent = Assert.Single(sink.Events);
        Assert.Equal(FulfillmentAuditEvent.CrossRegionOverrideAction, auditEvent.Action);
        Assert.Equal("staff-ops-7", auditEvent.ActorId);
        Assert.Equal(Order, auditEvent.Order);
        Assert.Equal(FulfillmentTestData.UsEast, auditEvent.FromStore);
        Assert.Equal(FulfillmentTestData.UsWest, auditEvent.ToStore);
        Assert.Equal(FixedNow, auditEvent.OccurredAt);
    }

    [Fact]
    [Requirement("CC-FUL-001")]
    public void Override_without_a_permission_proof_is_impossible()
    {
        var service = CreateService(new RecordingAuditSink());

        Assert.Throws<ArgumentNullException>(() =>
            service.ApplyCrossRegionOverride(RoutedToUsEast(), FulfillmentTestData.UsWest, null!));
    }

    [Fact]
    [Requirement("CC-FUL-001")]
    public void Override_permission_requires_a_staff_actor()
    {
        Assert.Throws<ArgumentException>(() => OverrideAuthorization.IssuedTo(" "));
    }

    [Fact]
    [Requirement("CC-FUL-001")]
    [Requirement("CC-DSH-004")]
    public void Failed_audit_append_denies_the_override()
    {
        var sink = new RecordingAuditSink { AppendSucceeds = false };
        var service = CreateService(sink);

        var result = service.ApplyCrossRegionOverride(RoutedToUsEast(), FulfillmentTestData.UsWest, OpsPermission);

        Assert.False(result.IsApplied);
        Assert.Null(result.Assignment);
        Assert.Equal(OverrideDenialReason.AuditAppendFailed, result.Denial);
        Assert.Empty(sink.Events);
    }

    [Fact]
    [Requirement("CC-FUL-001")]
    [Requirement("CC-DSH-004")]
    public void Audit_sink_exception_denies_the_override_fail_closed()
    {
        var sink = new RecordingAuditSink { ThrowOnAppend = true };
        var service = CreateService(sink);

        var result = service.ApplyCrossRegionOverride(RoutedToUsEast(), FulfillmentTestData.UsWest, OpsPermission);

        Assert.False(result.IsApplied);
        Assert.Null(result.Assignment);
        Assert.Equal(OverrideDenialReason.AuditAppendFailed, result.Denial);
    }

    [Fact]
    [Requirement("CC-FUL-001")]
    public void Unknown_target_store_is_denied_and_nothing_is_audited_as_applied()
    {
        var sink = new RecordingAuditSink();
        var service = CreateService(sink);

        var result = service.ApplyCrossRegionOverride(
            RoutedToUsEast(), ColdStoreId.Parse("cs-nowhere"), OpsPermission);

        Assert.False(result.IsApplied);
        Assert.Null(result.Assignment);
        Assert.Equal(OverrideDenialReason.UnknownTargetStore, result.Denial);
        Assert.Empty(sink.Events);
    }

    [Fact]
    [Requirement("CC-FUL-001")]
    public void Store_lookup_failure_during_override_is_a_denial_never_a_bypass()
    {
        var sink = new RecordingAuditSink();
        var service = new OrderRoutingService(
            new ThrowingServingRegionSource(), sink, new FixedTimeProvider(FixedNow));

        var result = service.ApplyCrossRegionOverride(RoutedToUsEast(), FulfillmentTestData.UsWest, OpsPermission);

        Assert.False(result.IsApplied);
        Assert.Equal(OverrideDenialReason.EvaluationFailed, result.Denial);
        Assert.Empty(sink.Events);
    }
}
