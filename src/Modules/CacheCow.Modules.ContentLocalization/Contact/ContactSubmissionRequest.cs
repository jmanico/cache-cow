namespace CacheCow.Modules.ContentLocalization.Contact;

/// <summary>
/// The dedicated request DTO for POST /contact (SECURITY.md, Input validation
/// rule 3: dedicated DTOs only, never domain models). Bound exclusively from
/// the JSON body by <see cref="ContactRequestReader"/> under strict options —
/// unknown members rejected, strict numbers — which is the minimal-API
/// equivalent of an explicit <c>[FromBody]</c> source. Every property is
/// attacker-controlled until <see cref="ContactSubmission.TryCreate"/> and the
/// abuse heuristics pass.
/// </summary>
/// <param name="Name">Submitter display name.</param>
/// <param name="Email">Reply-to address; syntax-validated server-side, used in the notification BODY only.</param>
/// <param name="Topic">A member of the closed <see cref="ContactTopics"/> set.</param>
/// <param name="Message">Plain-text message.</param>
/// <param name="Website">
/// Honeypot field (CAPTCHA-equivalent first-party heuristic, SECURITY.md,
/// Input validation rule 10): rendered invisibly by the form, so a human
/// leaves it null/empty; any value marks the submission as automated and it
/// is rejected without processing.
/// </param>
/// <param name="FillTimeMs">
/// Milliseconds between form render and submit as reported by the first-party
/// client (minimum-fill-time heuristic). Client-reported and therefore
/// forgeable — a bot-filter heuristic, never a security control; submissions
/// faster than the configured minimum are rejected.
/// </param>
/// <param name="ChallengeResponse">
/// Opaque response for the CAPTCHA-equivalent challenge verifier
/// (<see cref="IAbuseChallengeVerifier"/>). The mechanism is an open decision
/// (issue 076); the field exists so a ratified verifier plugs in without a
/// contract change. Bounded and control-character-checked before the verifier
/// sees it.
/// </param>
public sealed record ContactSubmissionRequest(
    string? Name,
    string? Email,
    string? Topic,
    string? Message,
    string? Website,
    long? FillTimeMs,
    string? ChallengeResponse);
