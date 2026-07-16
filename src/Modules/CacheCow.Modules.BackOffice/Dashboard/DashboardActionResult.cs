using CacheCow.Modules.BackOffice.Rbac;

namespace CacheCow.Modules.BackOffice.Dashboard;

/// <summary>
/// How a dashboard module operation concluded. The endpoint layer maps these
/// onto RFC 9457 responses; the vocabulary deliberately separates the denial
/// classes so 404-not-403 hardening (SECURITY.md, Authentication rule 9) and
/// the step-up challenge (Authentication rule 2) can be presented differently
/// without the services knowing about HTTP.
/// </summary>
public enum DashboardActionStatus
{
    /// <summary>The operation completed; the result carries a value.</summary>
    Completed = 0,

    /// <summary>The request was structurally invalid (rejected, never coerced — SECURITY.md, Input validation rule 1).</summary>
    InvalidRequest = 1,

    /// <summary>
    /// The permission check denied (missing matrix, unknown role, ungranted
    /// permission, or missing/stale step-up re-authentication). The denial
    /// reason travels for the structured authz security event (SECURITY.md,
    /// Logging rule 3); the client sees only a generic response.
    /// </summary>
    Denied = 2,

    /// <summary>The addressed resource does not exist — or must be presented as if it did not (SECURITY.md, Authentication rule 9).</summary>
    NotFound = 3,

    /// <summary>The owning context rejected the requested change (e.g., an illegal CC-ORD-006 transition). No state changed.</summary>
    Conflict = 4,

    /// <summary>A dependency (port or audit sink) failed; the operation is denied with no partial effect (fail closed, SECURITY.md, Logging rule 2).</summary>
    Unavailable = 5,
}

/// <summary>Factory methods for <see cref="DashboardActionResult{TValue}"/> (kept non-generic to keep call sites terse).</summary>
public static class DashboardActionResult
{
    public static DashboardActionResult<TValue> Completed<TValue>(TValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new DashboardActionResult<TValue>(DashboardActionStatus.Completed, AccessDenialReason.None, value);
    }

    public static DashboardActionResult<TValue> Denied<TValue>(AccessDenialReason reason)
    {
        if (reason == AccessDenialReason.None)
        {
            throw new ArgumentOutOfRangeException(nameof(reason), "A denial requires a reason other than None.");
        }

        return new DashboardActionResult<TValue>(DashboardActionStatus.Denied, reason, default);
    }

    public static DashboardActionResult<TValue> InvalidRequest<TValue>() =>
        new(DashboardActionStatus.InvalidRequest, AccessDenialReason.None, default);

    public static DashboardActionResult<TValue> NotFound<TValue>() =>
        new(DashboardActionStatus.NotFound, AccessDenialReason.None, default);

    public static DashboardActionResult<TValue> Conflict<TValue>() =>
        new(DashboardActionStatus.Conflict, AccessDenialReason.None, default);

    public static DashboardActionResult<TValue> Unavailable<TValue>() =>
        new(DashboardActionStatus.Unavailable, AccessDenialReason.None, default);
}

/// <summary>
/// The outcome of a dashboard module operation: a status, the denial reason
/// when <see cref="DashboardActionStatus.Denied"/>, and a value only when
/// <see cref="DashboardActionStatus.Completed"/>.
/// </summary>
public sealed class DashboardActionResult<TValue>
{
    internal DashboardActionResult(DashboardActionStatus status, AccessDenialReason denialReason, TValue? value)
    {
        Status = status;
        DenialReason = denialReason;
        Value = value;
    }

    public DashboardActionStatus Status { get; }

    /// <summary><see cref="AccessDenialReason.None"/> unless <see cref="Status"/> is <see cref="DashboardActionStatus.Denied"/>.</summary>
    public AccessDenialReason DenialReason { get; }

    /// <summary>The operation's value; non-null exactly when <see cref="Status"/> is <see cref="DashboardActionStatus.Completed"/>.</summary>
    public TValue? Value { get; }
}
