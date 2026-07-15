using CacheCow.Modules.BackOffice;
using CacheCow.Modules.BackOffice.Auditing;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.BackOffice.Tests;

/// <summary>
/// Issue 081: audit events carry every mandated field (actor, role, action,
/// object, before/after, server timestamp, correlation id), the retention
/// marker, and a closed, bounded, injection-rejecting shape (CC-DSH-004;
/// SECURITY.md, Logging rules 4–6; Input validation rule 1).
/// </summary>
public sealed class AuditEventTests
{
    [Fact]
    [Requirement("CC-DSH-004")]
    public void Created_event_carries_every_mandated_field()
    {
        var occurredAt = new DateTimeOffset(2026, 7, 15, 9, 30, 0, TimeSpan.Zero);

        var auditEvent = AuditEvent.Create(
            actor: "staff-fin-3",
            actorRole: "finance",
            action: "orders.refund",
            objectType: "order",
            objectId: "order-42",
            beforeSummary: "state=delivered",
            afterSummary: "state=refunded",
            occurredAt: occurredAt,
            correlationId: "corr-9",
            retentionClass: AuditRetentionClass.Financial);

        Assert.NotEqual(Guid.Empty, auditEvent.EventId);
        Assert.Equal("staff-fin-3", auditEvent.Actor);
        Assert.Equal("finance", auditEvent.ActorRole);
        Assert.Equal("orders.refund", auditEvent.Action);
        Assert.Equal("order", auditEvent.ObjectType);
        Assert.Equal("order-42", auditEvent.ObjectId);
        Assert.Equal("state=delivered", auditEvent.BeforeSummary);
        Assert.Equal("state=refunded", auditEvent.AfterSummary);
        Assert.Equal(occurredAt, auditEvent.OccurredAt);
        Assert.Equal("corr-9", auditEvent.CorrelationId);
        Assert.Equal(AuditRetentionClass.Financial, auditEvent.RetentionClass);
    }

    [Fact]
    [Requirement("CC-DSH-004")]
    [Requirement("CC-SEC-020")]
    public void Financial_actions_carry_the_ratified_seven_year_retention_marker()
    {
        var auditEvent = BackOfficeTestData.Event(retentionClass: AuditRetentionClass.Financial);

        Assert.Equal(AuditRetentionClass.Financial, auditEvent.RetentionClass);
        Assert.Equal(7, AuditRetention.RatifiedFinancialRetentionYears);
    }

    [Fact]
    [Requirement("CC-DSH-004")]
    public void Event_ids_are_unique_per_event_the_at_least_once_dedupe_key()
    {
        var first = BackOfficeTestData.Event();
        var second = BackOfficeTestData.Event();

        Assert.NotEqual(first.EventId, second.EventId);
    }

    [Theory]
    [Requirement("CC-DSH-004")]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_required_fields_reject_the_event(string missing)
    {
        Assert.Throws<AuditEventValidationException>(() => BackOfficeTestData.Event(actor: missing));
        Assert.Throws<AuditEventValidationException>(() => BackOfficeTestData.Event(actorRole: missing));
        Assert.Throws<AuditEventValidationException>(() => BackOfficeTestData.Event(action: missing));
        Assert.Throws<AuditEventValidationException>(() => BackOfficeTestData.Event(objectType: missing));
        Assert.Throws<AuditEventValidationException>(() => BackOfficeTestData.Event(objectId: missing));
        Assert.Throws<AuditEventValidationException>(() => BackOfficeTestData.Event(correlationId: missing));
    }

    [Fact]
    [Requirement("CC-DSH-004")]
    public void Summaries_may_be_empty_but_never_null()
    {
        var created = BackOfficeTestData.Event(beforeSummary: "", afterSummary: "state=received");
        Assert.Equal("", created.BeforeSummary);

        Assert.Throws<AuditEventValidationException>(() => BackOfficeTestData.Event(beforeSummary: null!));
        Assert.Throws<AuditEventValidationException>(() => BackOfficeTestData.Event(afterSummary: null!));
    }

    [Theory]
    [Requirement("CC-DSH-004")]
    [InlineData("line1\nline2")] // newline: the classic log-forging vector
    [InlineData("value\r")]
    [InlineData("value\t tab")]
    [InlineData("\u001b[31mansi")]
    public void Control_characters_are_rejected_not_sanitized_in_every_field(string injected)
    {
        Assert.Throws<AuditEventValidationException>(() => BackOfficeTestData.Event(actor: injected));
        Assert.Throws<AuditEventValidationException>(() => BackOfficeTestData.Event(actorRole: injected));
        Assert.Throws<AuditEventValidationException>(() => BackOfficeTestData.Event(action: injected));
        Assert.Throws<AuditEventValidationException>(() => BackOfficeTestData.Event(objectType: injected));
        Assert.Throws<AuditEventValidationException>(() => BackOfficeTestData.Event(objectId: injected));
        Assert.Throws<AuditEventValidationException>(() => BackOfficeTestData.Event(beforeSummary: injected));
        Assert.Throws<AuditEventValidationException>(() => BackOfficeTestData.Event(afterSummary: injected));
        Assert.Throws<AuditEventValidationException>(() => BackOfficeTestData.Event(correlationId: injected));
    }

    [Fact]
    [Requirement("CC-DSH-004")]
    public void Over_length_fields_are_rejected_not_truncated()
    {
        var longField = new string('a', AuditEvent.MaxFieldLength + 1);
        var longSummary = new string('a', AuditEvent.MaxSummaryLength + 1);

        Assert.Throws<AuditEventValidationException>(() => BackOfficeTestData.Event(action: longField));
        Assert.Throws<AuditEventValidationException>(() => BackOfficeTestData.Event(beforeSummary: longSummary));

        // At the bound is accepted: the bound is the contract, not a guess.
        var atBound = BackOfficeTestData.Event(
            action: new string('a', AuditEvent.MaxFieldLength),
            afterSummary: new string('b', AuditEvent.MaxSummaryLength));
        Assert.Equal(AuditEvent.MaxFieldLength, atBound.Action.Length);
    }

    [Fact]
    [Requirement("CC-DSH-004")]
    public void Retention_class_outside_the_closed_set_is_rejected()
    {
        Assert.Throws<AuditEventValidationException>(() =>
            BackOfficeTestData.Event(retentionClass: (AuditRetentionClass)42));
    }
}
