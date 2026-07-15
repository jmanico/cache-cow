namespace CacheCow.Modules.BackOffice.Auditing;

/// <summary>
/// One immutable audit record (CC-DSH-004; CC-ORD-006; SECURITY.md, Logging
/// rule 6): actor, actor role, action, object, before/after summaries, server
/// timestamp, correlation id. The shape is deliberately closed — every field
/// is a bounded, control-character-free string or a typed value, and no
/// free-form payload exists, so credentials, tokens, PANs, or log-injection
/// sequences cannot ride along (SECURITY.md, Logging rules 4–5; Authentication
/// rule 14). Validation rejects bad input outright rather than sanitizing it
/// into acceptance (SECURITY.md, Input validation rule 1).
///
/// The timestamp and actor identity are server-set by the emitting context,
/// never caller/client-supplied (SECURITY.md, Input validation rule 3).
/// Records are never mutated; corrections are new compensating events
/// (ARCHITECTURE.md, Dependency rule 6).
/// </summary>
public sealed record AuditEvent
{
    /// <summary>Maximum length of identifier-like fields (actor, role, action, object type/id, correlation id).</summary>
    public const int MaxFieldLength = 256;

    /// <summary>Maximum length of the before/after state summaries.</summary>
    public const int MaxSummaryLength = 2048;

    private AuditEvent(
        Guid eventId,
        string actor,
        string actorRole,
        string action,
        string objectType,
        string objectId,
        string beforeSummary,
        string afterSummary,
        DateTimeOffset occurredAt,
        string correlationId,
        AuditRetentionClass retentionClass)
    {
        EventId = eventId;
        Actor = actor;
        ActorRole = actorRole;
        Action = action;
        ObjectType = objectType;
        ObjectId = objectId;
        BeforeSummary = beforeSummary;
        AfterSummary = afterSummary;
        OccurredAt = occurredAt;
        CorrelationId = correlationId;
        RetentionClass = retentionClass;
    }

    /// <summary>Unique event identity, minted at creation — the deduplication key for at-least-once WORM replication (<see cref="IWormReplicationSink"/>).</summary>
    public Guid EventId { get; }

    /// <summary>The authenticated actor who performed the action, from server session state.</summary>
    public string Actor { get; }

    /// <summary>The RBAC role under which the action was authorized (CC-DSH-002).</summary>
    public string ActorRole { get; }

    /// <summary>The action performed, e.g. "orders.transition" or "staff-roles.change".</summary>
    public string Action { get; }

    /// <summary>The kind of object acted on, e.g. "order", "invoice", "employee-record".</summary>
    public string ObjectType { get; }

    /// <summary>The identity of the object acted on.</summary>
    public string ObjectId { get; }

    /// <summary>Bounded summary of the state before the action; empty when the action creates its object.</summary>
    public string BeforeSummary { get; }

    /// <summary>Bounded summary of the state after the action; empty when the action removes access to its object.</summary>
    public string AfterSummary { get; }

    /// <summary>Server-set time of the action (SECURITY.md, Input validation rule 3).</summary>
    public DateTimeOffset OccurredAt { get; }

    /// <summary>Correlation id linking the event to the request's structured logs (SECURITY.md, Logging rule 1).</summary>
    public string CorrelationId { get; }

    /// <summary>Retention marker: financial actions carry the ratified 7-year class (CC-DSH-004).</summary>
    public AuditRetentionClass RetentionClass { get; }

    /// <summary>
    /// Creates a validated audit event. Every string field is required
    /// (summaries may be empty but not null), bounded, and rejected if it
    /// carries control characters — the log-injection vector (SECURITY.md,
    /// Logging rule 5). <paramref name="occurredAt"/> must come from the
    /// emitting context's server clock (TimeProvider), never from a client.
    /// </summary>
    /// <exception cref="AuditEventValidationException">Any field fails validation; nothing partial is created (issue 081, Failure Behavior).</exception>
    public static AuditEvent Create(
        string actor,
        string actorRole,
        string action,
        string objectType,
        string objectId,
        string beforeSummary,
        string afterSummary,
        DateTimeOffset occurredAt,
        string correlationId,
        AuditRetentionClass retentionClass)
    {
        ValidateRequiredField(nameof(actor), actor);
        ValidateRequiredField(nameof(actorRole), actorRole);
        ValidateRequiredField(nameof(action), action);
        ValidateRequiredField(nameof(objectType), objectType);
        ValidateRequiredField(nameof(objectId), objectId);
        ValidateRequiredField(nameof(correlationId), correlationId);
        ValidateSummaryField(nameof(beforeSummary), beforeSummary);
        ValidateSummaryField(nameof(afterSummary), afterSummary);

        if (retentionClass != AuditRetentionClass.Standard && retentionClass != AuditRetentionClass.Financial)
        {
            throw new AuditEventValidationException(
                $"Audit event retention class {(int)retentionClass} is outside the closed set; rejected (SECURITY.md, Input validation rule 1).");
        }

        return new AuditEvent(
            Guid.NewGuid(),
            actor,
            actorRole,
            action,
            objectType,
            objectId,
            beforeSummary,
            afterSummary,
            occurredAt,
            correlationId,
            retentionClass);
    }

    private static void ValidateRequiredField(string fieldName, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new AuditEventValidationException(
                $"Audit event field '{fieldName}' is required; an event missing its mandated fields is rejected, never stored partially (CC-DSH-004; SECURITY.md, Logging rule 6).");
        }

        ValidateBounds(fieldName, value, MaxFieldLength);
    }

    private static void ValidateSummaryField(string fieldName, string value)
    {
        if (value is null)
        {
            throw new AuditEventValidationException(
                $"Audit event field '{fieldName}' must be present (empty is permitted, null is not) (CC-DSH-004).");
        }

        ValidateBounds(fieldName, value, MaxSummaryLength);
    }

    private static void ValidateBounds(string fieldName, string value, int maxLength)
    {
        if (value.Length > maxLength)
        {
            throw new AuditEventValidationException(
                $"Audit event field '{fieldName}' exceeds its {maxLength}-character bound; rejected, never truncated into acceptance (SECURITY.md, Input validation rule 1).");
        }

        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                throw new AuditEventValidationException(
                    $"Audit event field '{fieldName}' contains a control character — a log-injection vector — and is rejected (SECURITY.md, Logging rule 5).");
            }
        }
    }
}
