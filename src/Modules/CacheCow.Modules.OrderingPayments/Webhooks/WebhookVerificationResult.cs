namespace CacheCow.Modules.OrderingPayments.Webhooks;

/// <summary>Why a webhook delivery was rejected (issue 041). Every value denotes a security event the host logs and alerts on (SECURITY.md, Secret handling rule 9; Logging rule 3).</summary>
public enum WebhookRejectionReason
{
    /// <summary>No signature was presented.</summary>
    MissingSignature = 0,

    /// <summary>The presented signature is not a well-formed HMAC-SHA256 value (bad hex, wrong length).</summary>
    MalformedSignature = 1,

    /// <summary>The signature does not match any acceptable secret's HMAC over the raw body.</summary>
    InvalidSignature = 2,

    /// <summary>No signing secret could be resolved for the processor (unconfigured or provider failure) — fail closed, never skip verification.</summary>
    SigningSecretUnavailable = 3,

    /// <summary>No sender timestamp was presented, so the replay bound cannot be enforced.</summary>
    MissingTimestamp = 4,

    /// <summary>The sender timestamp is outside the configured max event age (either direction).</summary>
    StaleTimestamp = 5,

    /// <summary>No event id (replay nonce) was presented.</summary>
    MissingEventId = 6,

    /// <summary>The event id was already seen within the replay bounds (CC-SEC-014).</summary>
    ReplayedEventId = 7,

    /// <summary>An exception occurred inside the verification path — denied, never degraded to acceptance (SECURITY.md, Logging rule 2).</summary>
    VerificationError = 8,
}

/// <summary>
/// Typed rejection of a webhook delivery, shaped for the host to log as a
/// structured security event (issue 041, AC-02; SECURITY.md, Logging rule 3).
/// Carries no body content, no presented signature, and no secret material
/// (SECURITY.md, Logging rule 4).
/// </summary>
public sealed record WebhookRejection(
    string ProcessorName,
    WebhookRejectionReason Reason,
    DateTimeOffset RejectedAt);

/// <summary>
/// Outcome of <see cref="WebhookVerifier.Verify"/>: exactly one of
/// <see cref="Event"/> (verified) or <see cref="Rejection"/> (denied) is set.
/// Only the verifier constructs instances — host code cannot fabricate a
/// "verified" outcome.
/// </summary>
public sealed class WebhookVerificationResult
{
    private WebhookVerificationResult(VerifiedProcessorEvent? verifiedEvent, WebhookRejection? rejection)
    {
        Event = verifiedEvent;
        Rejection = rejection;
    }

    public bool IsVerified => Event is not null;

    /// <summary>The verified event; non-null exactly when <see cref="IsVerified"/>.</summary>
    public VerifiedProcessorEvent? Event { get; }

    /// <summary>The typed rejection; non-null exactly when not verified.</summary>
    public WebhookRejection? Rejection { get; }

    internal static WebhookVerificationResult Verified(VerifiedProcessorEvent verifiedEvent) =>
        new(verifiedEvent, null);

    internal static WebhookVerificationResult Rejected(WebhookRejection rejection) =>
        new(null, rejection);
}
