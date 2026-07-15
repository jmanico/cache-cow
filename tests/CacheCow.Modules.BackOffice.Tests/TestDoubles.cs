using CacheCow.Modules.BackOffice.Auditing;
using CacheCow.Modules.BackOffice.Rbac;

namespace CacheCow.Modules.BackOffice.Tests;

/// <summary>Deterministic clock for step-up recency tests.</summary>
internal sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset now;

    public FixedTimeProvider(DateTimeOffset now)
    {
        this.now = now;
    }

    public override DateTimeOffset GetUtcNow() => now;
}

/// <summary>Records every event delivered toward WORM storage (issue 081 sink contract).</summary>
internal sealed class RecordingWormReplicationSink : IWormReplicationSink
{
    private readonly List<AuditEvent> replicated = [];

    public IReadOnlyList<AuditEvent> Replicated => replicated;

    public void Replicate(AuditEvent auditEvent)
    {
        lock (replicated)
        {
            replicated.Add(auditEvent);
        }
    }
}

/// <summary>A sink whose delivery always fails, for durable-first at-least-once tests.</summary>
internal sealed class ThrowingWormReplicationSink : IWormReplicationSink
{
    public void Replicate(AuditEvent auditEvent) =>
        throw new InvalidOperationException("Replication target unavailable.");
}

/// <summary>A matrix provider that fails, for fail-closed authorization tests (SECURITY.md, Logging rule 2).</summary>
internal sealed class ThrowingMatrixProvider : IRolePermissionMatrixProvider
{
    public RolePermissionMatrix? Current =>
        throw new InvalidOperationException("Matrix source unavailable.");
}

internal static class BackOfficeTestData
{
    /// <summary>A valid audit event; every field individually overridable.</summary>
    public static AuditEvent Event(
        string actor = "staff-ops-7",
        string actorRole = "ops-agent",
        string action = "orders.transition",
        string objectType = "order",
        string objectId = "order-1",
        string beforeSummary = "state=received",
        string afterSummary = "state=confirmed",
        DateTimeOffset? occurredAt = null,
        string correlationId = "corr-1",
        AuditRetentionClass retentionClass = AuditRetentionClass.Standard) =>
        AuditEvent.Create(
            actor,
            actorRole,
            action,
            objectType,
            objectId,
            beforeSummary,
            afterSummary,
            occurredAt ?? new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero),
            correlationId,
            retentionClass);
}
