using CacheCow.Modules.BackOffice.Auditing;
using CacheCow.Modules.BackOffice.Rbac;

namespace CacheCow.Modules.BackOffice.Dashboard;

/// <summary>How a command port answered, for the audit outcome record.</summary>
internal enum DashboardAuditOutcome
{
    /// <summary>The port applied the change.</summary>
    Applied,

    /// <summary>The owning context refused the change; no state moved.</summary>
    Rejected,

    /// <summary>The port faulted; the outcome is UNKNOWN, not "no change".</summary>
    Unavailable,
}

/// <summary>
/// Writes the audit trail for privileged dashboard commands (CC-DSH-004;
/// SECURITY.md, Logging rule 6) under a strict append-BEFORE-effect discipline.
///
/// Why two events per command. The Wholesale context's onboarding workflow can
/// append exactly one event because it holds the aggregate and checks
/// transition legality itself before appending. This module cannot and must
/// not: transition legality belongs to the Ordering &amp; Payments state
/// machine behind the port (issue 082, Trust Boundary), so whether the change
/// takes effect is unknowable until the port has been called — i.e. until
/// after the audit append that append-before-effect requires. So:
///
/// <list type="number">
/// <item><see cref="AppendAttempt"/> records the privileged action BEFORE the
/// port is touched. Its after-summary is framed as <c>requested ...</c>, never
/// as an accomplished state, so this record is truthful standing alone — it
/// says a staff member invoked the action, which is itself the audited fact
/// (CC-DSH-004: every privileged ACTION writes an event). If it throws, the
/// caller denies and the port is never invoked: no effect can exist without
/// its audit record, which is the entire point of the ordering.</item>
/// <item><see cref="TryAppendOutcome"/> records what the port then answered,
/// as a separate appended event — the compensating-event mechanism SECURITY.md
/// Logging rule 6 and ARCHITECTURE.md Dependency rule 6 mandate ("corrections
/// are new records"). Nothing is ever mutated to correct the attempt
/// record.</item>
/// </list>
///
/// The outcome append is best-effort BY DESIGN, and only this one is: it runs
/// after the effect has already happened, so throwing could not undo it, and
/// letting it throw would replace a completed action's response with an error
/// that falsely implies nothing happened. The attempt record already
/// guarantees no unaudited effect exists. A failure here is a monitoring
/// concern (SECURITY.md, Logging rule 3), not a request-path failure.
/// </summary>
internal sealed class DashboardAuditWriter
{
    private readonly IAuditEventSink sink;
    private readonly TimeProvider timeProvider;

    internal DashboardAuditWriter(IAuditEventSink sink, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.sink = sink;
        this.timeProvider = timeProvider;
    }

    /// <summary>
    /// Appends the pre-effect record of a privileged action. THROWS on any
    /// failure — callers treat a throw as denial and MUST NOT proceed to the
    /// port (issue 081, Failure Behavior; issue 085 Failure Behavior: "If the
    /// audit write fails, the action fails").
    /// </summary>
    internal void AppendAttempt(
        StaffContext staff,
        string action,
        string objectType,
        string objectId,
        string beforeSummary,
        string requestedSummary,
        string correlationId,
        AuditRetentionClass retentionClass) =>
        sink.Append(AuditEvent.Create(
            staff.ActorId,
            staff.RoleName,
            action,
            objectType,
            objectId,
            beforeSummary,
            // Framed as a request, not an accomplishment: at append time the
            // owning context has not yet ruled on it.
            $"requested {requestedSummary}",
            timeProvider.GetUtcNow(),
            correlationId,
            retentionClass));

    /// <summary>
    /// Appends the post-effect outcome record. Never throws (see the type
    /// remarks); the action's authoritative record is the attempt event.
    /// </summary>
    internal void TryAppendOutcome(
        StaffContext staff,
        string action,
        string objectType,
        string objectId,
        string beforeSummary,
        string afterSummary,
        string correlationId,
        AuditRetentionClass retentionClass,
        DashboardAuditOutcome outcome)
    {
        // Deliberate catch-all: this runs after the effect and must not turn a
        // completed action into a failed response, nor mask the real result.
#pragma warning disable CA1031
        try
        {
            sink.Append(AuditEvent.Create(
                staff.ActorId,
                staff.RoleName,
                $"{action}.{OutcomeSuffix(outcome)}",
                objectType,
                objectId,
                beforeSummary,
                afterSummary,
                timeProvider.GetUtcNow(),
                correlationId,
                retentionClass));
        }
        catch (Exception)
        {
            // Swallowed by design.
        }
#pragma warning restore CA1031
    }

    private static string OutcomeSuffix(DashboardAuditOutcome outcome) => outcome switch
    {
        DashboardAuditOutcome.Applied => "applied",
        DashboardAuditOutcome.Rejected => "rejected",
        _ => "unavailable",
    };
}
