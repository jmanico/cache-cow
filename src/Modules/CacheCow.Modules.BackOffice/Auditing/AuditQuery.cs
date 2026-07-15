namespace CacheCow.Modules.BackOffice.Auditing;

/// <summary>
/// Read-side filter for the audit store: by actor, by object, by time window
/// (issue 081; readers are role-gated dashboard views, issue 080). All
/// criteria are optional and conjunctive; an empty query matches everything.
/// Matching is exact/ordinal — no patterns, so a filter value can never
/// become an injection vector.
/// </summary>
public sealed record AuditQuery
{
    /// <summary>Matches events whose <see cref="AuditEvent.Actor"/> equals this value exactly; null matches any actor.</summary>
    public string? Actor { get; init; }

    /// <summary>Matches events whose <see cref="AuditEvent.ObjectType"/> equals this value exactly; null matches any type.</summary>
    public string? ObjectType { get; init; }

    /// <summary>Matches events whose <see cref="AuditEvent.ObjectId"/> equals this value exactly; null matches any object.</summary>
    public string? ObjectId { get; init; }

    /// <summary>Inclusive lower bound on <see cref="AuditEvent.OccurredAt"/>; null for unbounded.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Inclusive upper bound on <see cref="AuditEvent.OccurredAt"/>; null for unbounded.</summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>True when the event satisfies every supplied criterion.</summary>
    public bool Matches(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        return (Actor is null || string.Equals(Actor, auditEvent.Actor, StringComparison.Ordinal))
            && (ObjectType is null || string.Equals(ObjectType, auditEvent.ObjectType, StringComparison.Ordinal))
            && (ObjectId is null || string.Equals(ObjectId, auditEvent.ObjectId, StringComparison.Ordinal))
            && (From is null || auditEvent.OccurredAt >= From)
            && (To is null || auditEvent.OccurredAt <= To);
    }
}
