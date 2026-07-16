using Microsoft.AspNetCore.Http;

namespace CacheCow.Modules.ContentLocalization.Contact;

/// <summary>
/// RFC 9457 problem responses for the contact endpoint: generic bodies only —
/// no stack traces, no internal identifiers, and NEVER an echo of submitted
/// values (SECURITY.md, Logging rule 1). One identical rejection body covers
/// schema, validation, and every abuse-control outcome, so automated abusers
/// get no oracle distinguishing which control tripped; the specific reason is
/// a structured security log event, not response content. Wording follows
/// DESIGN.md §9: what happened and what to do next, no apologies, no puns
/// (§5.4 — zero puns in error recovery). Locale-aware presentation of these
/// errors is the form UI's job (AC-08); the problem body is machine-facing.
/// </summary>
internal static class ContactProblems
{
    internal static IResult Rejected() =>
        Results.Problem(
            title: "Submission not accepted.",
            detail: "The submission failed validation or abuse checks. Review the form fields and try again.",
            statusCode: StatusCodes.Status400BadRequest);

    internal static IResult UnsupportedMediaType() =>
        Results.Problem(
            title: "Unsupported media type.",
            detail: "This endpoint accepts application/json request bodies only.",
            statusCode: StatusCodes.Status415UnsupportedMediaType);

    /// <summary>The endpoint is not configured (options or verifier missing): fail closed, process nothing (SECURITY.md, Logging rule 2).</summary>
    internal static IResult Unavailable() =>
        Results.Problem(
            title: "Service unavailable.",
            detail: "The contact form is temporarily unavailable. Try again later.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
}
