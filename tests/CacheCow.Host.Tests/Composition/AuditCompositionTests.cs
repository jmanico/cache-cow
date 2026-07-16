using CacheCow.Modules.BackOffice;
using CacheCow.Modules.BackOffice.Auditing;
using CacheCow.Modules.Fulfillment;
using CacheCow.Modules.Fulfillment.Auditing;
using CacheCow.Modules.OrderingPayments.Orders;
using CacheCow.Modules.WholesaleB2B.Partners;
using CacheCow.SharedKernel.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CacheCow.Host.Tests.Composition;

/// <summary>
/// Host composition: an audit event emitted through EACH module-local audit
/// port (OrderingPayments IAuditSink, Fulfillment IFulfillmentAuditSink,
/// WholesaleB2B IPartnerAuditSink) lands in the BackOffice append-only store
/// with its fields mapped faithfully (CC-DSH-004, CC-ORD-006; SECURITY.md,
/// Logging rule 6; ARCHITECTURE.md, Dependency rules 6 and 9 — adapters are
/// host wiring).
/// </summary>
public sealed class AuditCompositionTests
{
    [Fact]
    [Requirement("CC-DSH-004")]
    [Requirement("CC-ORD-006")]
    public void Order_state_transition_audit_lands_in_backoffice_store_with_faithful_fields()
    {
        using var factory = TestHostBuilder.Create();
        var sink = factory.Services.GetRequiredService<IAuditSink>();
        var store = factory.Services.GetRequiredService<IAuditStore>();

        var orderId = OrderId.New();
        var occurredAt = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        sink.Append(new OrderAuditEvent(
            "ops-agent-1", "orders.transition", orderId, OrderState.Received, OrderState.Confirmed, occurredAt));

        var stored = Assert.Single(store.Query(new AuditQuery { ObjectId = orderId.ToString() }));
        Assert.Equal("ops-agent-1", stored.Actor);
        Assert.Equal("orders.transition", stored.Action);
        Assert.Equal("order", stored.ObjectType);
        Assert.Equal(orderId.ToString(), stored.ObjectId);
        Assert.Equal(nameof(OrderState.Received), stored.BeforeSummary);
        Assert.Equal(nameof(OrderState.Confirmed), stored.AfterSummary);
        Assert.Equal(occurredAt, stored.OccurredAt);
        Assert.False(string.IsNullOrWhiteSpace(stored.CorrelationId));

        // Order transitions belong to the financial stream (7-year retention,
        // ratified 2026-07-15).
        Assert.Equal(AuditRetentionClass.Financial, stored.RetentionClass);
    }

    [Fact]
    [Requirement("CC-DSH-004")]
    [Requirement("CC-FUL-001")]
    public void Cross_region_override_audit_lands_in_backoffice_store_and_reports_success()
    {
        using var factory = TestHostBuilder.Create();
        var sink = factory.Services.GetRequiredService<IFulfillmentAuditSink>();
        var store = factory.Services.GetRequiredService<IAuditStore>();

        var occurredAt = new DateTimeOffset(2026, 7, 15, 13, 0, 0, TimeSpan.Zero);
        var appended = sink.TryAppend(FulfillmentAuditEvent.CrossRegionOverride(
            "ops-agent-2",
            OrderReference.Parse("order-77"),
            ColdStoreId.Parse("cs-eu-1"),
            ColdStoreId.Parse("cs-us-1"),
            occurredAt));

        // The unconfigured module default always reported false; the adapter
        // proves the real store is wired (a false here would deny every
        // cross-region override forever).
        Assert.True(appended);

        var stored = Assert.Single(store.Query(new AuditQuery { ObjectId = "order-77" }));
        Assert.Equal("ops-agent-2", stored.Actor);
        Assert.Equal(FulfillmentAuditEvent.CrossRegionOverrideAction, stored.Action);
        Assert.Equal("order", stored.ObjectType);
        Assert.Equal("cs-eu-1", stored.BeforeSummary);
        Assert.Equal("cs-us-1", stored.AfterSummary);
        Assert.Equal(occurredAt, stored.OccurredAt);
        Assert.Equal(AuditRetentionClass.Standard, stored.RetentionClass);
    }

    [Fact]
    [Requirement("CC-DSH-004")]
    [Requirement("CC-WHS-002")]
    public void Partner_tenancy_transition_audit_lands_in_backoffice_store_with_role()
    {
        using var factory = TestHostBuilder.Create();
        var sink = factory.Services.GetRequiredService<IPartnerAuditSink>();
        var store = factory.Services.GetRequiredService<IAuditStore>();

        var occurredAt = new DateTimeOffset(2026, 7, 15, 14, 0, 0, TimeSpan.Zero);
        sink.Append(new PartnerAuditEvent(
            "staff-3",
            "admin",
            PartnerOnboardingWorkflow.ApproveAction,
            PartnerId.Parse("partner-9"),
            PartnerOnboardingState.Submitted,
            PartnerOnboardingState.Approved,
            occurredAt));

        var stored = Assert.Single(store.Query(new AuditQuery { ObjectId = "partner-9" }));
        Assert.Equal("staff-3", stored.Actor);
        Assert.Equal("admin", stored.ActorRole);
        Assert.Equal(PartnerOnboardingWorkflow.ApproveAction, stored.Action);
        Assert.Equal("partner", stored.ObjectType);
        Assert.Equal(nameof(PartnerOnboardingState.Submitted), stored.BeforeSummary);
        Assert.Equal(nameof(PartnerOnboardingState.Approved), stored.AfterSummary);
        Assert.Equal(occurredAt, stored.OccurredAt);
    }

    [Fact]
    [Requirement("CC-DSH-004")]
    public void Invalid_audit_fields_are_rejected_and_nothing_partial_is_stored()
    {
        using var factory = TestHostBuilder.Create();
        var sink = factory.Services.GetRequiredService<IAuditSink>();
        var store = factory.Services.GetRequiredService<IAuditStore>();

        // A control character is a log-injection vector: the append throws
        // (denying the audited action) and nothing is retained.
        Assert.Throws<AuditEventValidationException>(() => sink.Append(new OrderAuditEvent(
            "actor\r\ninjected", "orders.transition", OrderId.New(),
            OrderState.Received, OrderState.Confirmed, DateTimeOffset.UtcNow)));

        Assert.Empty(store.Query(new AuditQuery()));
    }
}
