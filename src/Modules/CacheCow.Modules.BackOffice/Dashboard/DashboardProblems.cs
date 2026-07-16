using CacheCow.Modules.BackOffice.Rbac;
using Microsoft.AspNetCore.Http;

namespace CacheCow.Modules.BackOffice.Dashboard;

/// <summary>
/// RFC 9457 problem-details responses for the dashboard endpoints
/// (SECURITY.md, Logging rule 1): generic bodies only — no stack traces,
/// exception messages, SQL, internal identifiers, or port responses ever reach
/// a client (issue 082, AC-07; issue 084/085 Failure Behavior). No humor
/// appears in any of these strings: error recovery and money movement are
/// outside the pun budget (DESIGN.md §5.4, §9).
///
/// Denial mapping (the reason why <see cref="DashboardActionStatus"/> keeps
/// the denial classes apart):
/// <list type="bullet">
/// <item>every authorization denial is <see cref="NotFound"/> — 404, never a
/// 403 that would confirm the resource exists or that the role is merely
/// under-privileged (SECURITY.md, Authentication rule 9; issue 082 AC-06,
/// issue 084 AC-04, issue 085 AC-06);</item>
/// <item>EXCEPT a missing/stale step-up re-authentication, which is
/// <see cref="StepUpRequired"/> — 403. The actor demonstrably holds the
/// permission (the matrix granted it) and needs to re-authenticate, so 404
/// would be both wrong and an unrecoverable dead end. It leaks nothing about
/// the resource: the permission check runs BEFORE the resource is ever looked
/// up, so this response is identical for an order that does not exist
/// (SECURITY.md, Authentication rule 2).</item>
/// </list>
/// </summary>
internal static class DashboardProblems
{
    internal static IResult Unauthorized() =>
        Results.Problem(title: "Unauthorized.", statusCode: StatusCodes.Status401Unauthorized);

    internal static IResult NotFound() =>
        Results.Problem(title: "Not found.", statusCode: StatusCodes.Status404NotFound);

    internal static IResult StepUpRequired() =>
        Results.Problem(
            title: "Re-authentication required.",
            detail: "This action requires a recent re-authentication (SECURITY.md, Authentication rule 2).",
            statusCode: StatusCodes.Status403Forbidden);

    internal static IResult Validation() =>
        Results.Problem(title: "Invalid request.", statusCode: StatusCodes.Status400BadRequest);

    internal static IResult UnsupportedMediaType() =>
        Results.Problem(
            title: "Unsupported media type.",
            detail: "This endpoint accepts application/json request bodies only.",
            statusCode: StatusCodes.Status415UnsupportedMediaType);

    /// <summary>The owning context refused the change (e.g. an illegal CC-ORD-006 transition); no state changed.</summary>
    internal static IResult Conflict() =>
        Results.Problem(
            title: "The requested change is not permitted in the resource's current state.",
            statusCode: StatusCodes.Status409Conflict);

    internal static IResult Unavailable() =>
        Results.Problem(title: "Service unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);

    /// <summary>
    /// Maps a service result onto its response. Only
    /// <see cref="DashboardActionStatus.Completed"/> has a body; everything
    /// else is a generic problem.
    /// </summary>
    internal static IResult From<TValue>(DashboardActionResult<TValue> result, Func<TValue, IResult> onCompleted)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onCompleted);

        return result.Status switch
        {
            DashboardActionStatus.Completed => onCompleted(result.Value!),
            DashboardActionStatus.InvalidRequest => Validation(),
            DashboardActionStatus.Denied => FromDenial(result.DenialReason),
            DashboardActionStatus.NotFound => NotFound(),
            DashboardActionStatus.Conflict => Conflict(),
            DashboardActionStatus.Unavailable => Unavailable(),

            // An unmapped status is a defect, and an unmapped status must not
            // become a success: fail closed (SECURITY.md, Logging rule 2).
            _ => Unavailable(),
        };
    }

    private static IResult FromDenial(AccessDenialReason reason) => reason switch
    {
        AccessDenialReason.ReauthenticationMissing or AccessDenialReason.ReauthenticationStale =>
            StepUpRequired(),

        // StepUpPolicyNotConfigured is deliberately NOT a step-up challenge:
        // the policy is missing configuration, so re-authenticating would
        // loop forever. It is an ordinary 404 like every other denial.
        _ => NotFound(),
    };
}
