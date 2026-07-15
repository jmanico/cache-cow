using Microsoft.AspNetCore.Http;

namespace CacheCow.Modules.WholesaleB2B.Api;

/// <summary>
/// RFC 9457 problem-details responses for the B2B API (CC-API-006): generic
/// bodies only — no stack traces, exception messages, internal identifiers, or
/// catalog enumeration in any error (SECURITY.md, Logging rule 1). Denied
/// resource access is 404, never a 403 that confirms existence (SECURITY.md,
/// Authentication rule 9; CC-MKT-004).
/// </summary>
internal static class B2BProblems
{
    internal static IResult Unauthorized() =>
        Results.Problem(title: "Unauthorized.", statusCode: StatusCodes.Status401Unauthorized);

    internal static IResult MissingScope() =>
        Results.Problem(
            title: "Forbidden.",
            detail: "The access token does not carry the scope this endpoint requires (CC-API-004).",
            statusCode: StatusCodes.Status403Forbidden);

    internal static IResult NotFound() =>
        Results.Problem(title: "Not found.", statusCode: StatusCodes.Status404NotFound);

    internal static IResult Validation(string detail) =>
        Results.Problem(
            title: "Invalid request.",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest);

    internal static IResult SchemaViolation() =>
        Validation("The request body does not match the published schema for this endpoint (CC-API-006).");

    internal static IResult UnsupportedMediaType() =>
        Results.Problem(
            title: "Unsupported media type.",
            detail: "This endpoint accepts application/json request bodies only.",
            statusCode: StatusCodes.Status415UnsupportedMediaType);

    internal static IResult IdempotencyConflict() =>
        Results.Problem(
            title: "Idempotency-Key conflict.",
            detail: "This Idempotency-Key was already used with a different request (CC-SEC-015).",
            statusCode: StatusCodes.Status409Conflict);

    internal static IResult GatingUnavailable() =>
        Results.Problem(
            title: "Service unavailable.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
}
