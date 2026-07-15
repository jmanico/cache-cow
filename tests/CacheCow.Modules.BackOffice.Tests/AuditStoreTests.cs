using System.Reflection;
using CacheCow.Modules.BackOffice.Auditing;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.BackOffice.Tests;

/// <summary>
/// Issue 081: the audit store's only write operation is Append — mutation and
/// removal are unrepresentable on its public surface (reflection-verified),
/// reads are query-only snapshots, financial events carry the 7-year marker,
/// every append reaches the WORM replication sink, and concurrent appends are
/// all retained (CC-DSH-004; CC-SEC-020; SECURITY.md, Logging rule 6).
/// </summary>
public sealed class AuditStoreTests
{
    private static readonly string[] ForbiddenMemberNameFragments =
        ["update", "delete", "remove", "clear", "truncate", "mutate", "replace", "purge", "erase", "set"];

    private static InMemoryAuditStore Store(IWormReplicationSink? sink = null) =>
        new(sink ?? new RecordingWormReplicationSink());

    [Fact]
    [Requirement("CC-SEC-020")]
    [Requirement("CC-DSH-004")]
    public void Store_contract_declares_append_and_query_only_no_mutating_member_exists()
    {
        // The whole reachable contract: IAuditStore plus every interface it
        // inherits (the write-only IAuditEventSink face included).
        var contractMembers = new[] { typeof(IAuditStore) }
            .Concat(typeof(IAuditStore).GetInterfaces())
            .SelectMany(t => t.GetMembers())
            .ToArray();

        var memberNames = contractMembers.Select(m => m.Name).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["Append", "Query"], memberNames);

        foreach (var name in memberNames)
        {
            Assert.DoesNotContain(
                ForbiddenMemberNameFragments,
                fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    [Requirement("CC-SEC-020")]
    public void In_memory_store_public_surface_has_no_mutating_or_removing_member()
    {
        var publicMembers = typeof(InMemoryAuditStore)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => m is not ConstructorInfo)
            .ToArray();

        var methodNames = publicMembers.Select(m => m.Name).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["Append", "Query"], methodNames);

        // No public field or property exposes internal storage to mutation.
        Assert.DoesNotContain(publicMembers, m => m is FieldInfo or PropertyInfo);
    }

    [Fact]
    [Requirement("CC-DSH-004")]
    public void Appended_events_are_returned_by_query_with_fields_intact()
    {
        var store = Store();
        var auditEvent = BackOfficeTestData.Event();

        store.Append(auditEvent);
        var results = store.Query(new AuditQuery());

        var stored = Assert.Single(results);
        Assert.Equal(auditEvent, stored);
    }

    [Fact]
    [Requirement("CC-DSH-004")]
    public void Query_filters_by_actor()
    {
        var store = Store();
        store.Append(BackOfficeTestData.Event(actor: "staff-a"));
        store.Append(BackOfficeTestData.Event(actor: "staff-b"));

        var results = store.Query(new AuditQuery { Actor = "staff-a" });

        Assert.Equal(["staff-a"], results.Select(e => e.Actor).ToArray());
    }

    [Fact]
    [Requirement("CC-DSH-004")]
    public void Query_filters_by_object_type_and_id()
    {
        var store = Store();
        store.Append(BackOfficeTestData.Event(objectType: "order", objectId: "order-1"));
        store.Append(BackOfficeTestData.Event(objectType: "order", objectId: "order-2"));
        store.Append(BackOfficeTestData.Event(objectType: "invoice", objectId: "order-1"));

        var results = store.Query(new AuditQuery { ObjectType = "order", ObjectId = "order-1" });

        var stored = Assert.Single(results);
        Assert.Equal("order", stored.ObjectType);
        Assert.Equal("order-1", stored.ObjectId);
    }

    [Fact]
    [Requirement("CC-DSH-004")]
    public void Query_filters_by_inclusive_time_window()
    {
        var store = Store();
        var noon = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        store.Append(BackOfficeTestData.Event(objectId: "early", occurredAt: noon.AddHours(-2)));
        store.Append(BackOfficeTestData.Event(objectId: "at-from", occurredAt: noon.AddHours(-1)));
        store.Append(BackOfficeTestData.Event(objectId: "inside", occurredAt: noon));
        store.Append(BackOfficeTestData.Event(objectId: "at-to", occurredAt: noon.AddHours(1)));
        store.Append(BackOfficeTestData.Event(objectId: "late", occurredAt: noon.AddHours(2)));

        var results = store.Query(new AuditQuery { From = noon.AddHours(-1), To = noon.AddHours(1) });

        Assert.Equal(["at-from", "inside", "at-to"], results.Select(e => e.ObjectId).ToArray());
    }

    [Fact]
    [Requirement("CC-SEC-020")]
    public void Query_results_are_read_only_snapshots_detached_from_the_store()
    {
        var store = Store();
        store.Append(BackOfficeTestData.Event(objectId: "order-1"));

        var snapshot = store.Query(new AuditQuery());

        var asCollection = Assert.IsAssignableFrom<ICollection<AuditEvent>>(snapshot);
        Assert.True(asCollection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => asCollection.Add(BackOfficeTestData.Event()));
        Assert.Throws<NotSupportedException>(() => asCollection.Clear());

        // A snapshot never tracks later appends: it is a copy, not a view.
        store.Append(BackOfficeTestData.Event(objectId: "order-2"));
        Assert.Single(snapshot);
        Assert.Equal(2, store.Query(new AuditQuery()).Count);
    }

    [Fact]
    [Requirement("CC-DSH-004")]
    [Requirement("CC-SEC-020")]
    public void Financial_events_keep_their_retention_marker_through_the_store()
    {
        var store = Store();
        store.Append(BackOfficeTestData.Event(objectId: "refund-1", retentionClass: AuditRetentionClass.Financial));
        store.Append(BackOfficeTestData.Event(objectId: "view-1"));

        var stored = store.Query(new AuditQuery { ObjectId = "refund-1" });

        Assert.Equal(AuditRetentionClass.Financial, Assert.Single(stored).RetentionClass);
    }

    [Fact]
    [Requirement("CC-SEC-020")]
    public void Every_append_is_delivered_to_the_worm_replication_sink()
    {
        var sink = new RecordingWormReplicationSink();
        var store = Store(sink);
        var first = BackOfficeTestData.Event(objectId: "order-1");
        var second = BackOfficeTestData.Event(objectId: "order-2", retentionClass: AuditRetentionClass.Financial);

        store.Append(first);
        store.Append(second);

        Assert.Equal([first.EventId, second.EventId], sink.Replicated.Select(e => e.EventId).ToArray());
    }

    [Fact]
    [Requirement("CC-SEC-020")]
    public void A_failing_replication_sink_never_loses_the_retained_event()
    {
        // Durable-first at-least-once: the event is retained before delivery,
        // so a sink failure surfaces to the caller but the record survives
        // for later re-replication (issue 081, Failure Behavior).
        var store = Store(new ThrowingWormReplicationSink());
        var auditEvent = BackOfficeTestData.Event();

        Assert.Throws<InvalidOperationException>(() => store.Append(auditEvent));

        Assert.Equal(auditEvent, Assert.Single(store.Query(new AuditQuery())));
    }

    [Fact]
    [Requirement("CC-DSH-004")]
    [Requirement("CC-SEC-020")]
    public void Concurrent_appends_are_all_retained()
    {
        var store = Store();
        const int writers = 8;
        const int eventsPerWriter = 50;

        Parallel.For(0, writers, writer =>
        {
            for (var i = 0; i < eventsPerWriter; i++)
            {
                store.Append(BackOfficeTestData.Event(objectId: $"order-{writer}-{i}"));
            }
        });

        var stored = store.Query(new AuditQuery());
        Assert.Equal(writers * eventsPerWriter, stored.Count);
        Assert.Equal(writers * eventsPerWriter, stored.Select(e => e.EventId).Distinct().Count());
    }

    [Fact]
    [Requirement("CC-DSH-004")]
    public void Null_events_and_null_queries_are_rejected()
    {
        var store = Store();

        Assert.Throws<ArgumentNullException>(() => store.Append(null!));
        Assert.Throws<ArgumentNullException>(() => store.Query(null!));
    }
}
