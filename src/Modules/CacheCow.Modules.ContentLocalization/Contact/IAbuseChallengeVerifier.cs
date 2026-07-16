namespace CacheCow.Modules.ContentLocalization.Contact;

/// <summary>Outcome of the CAPTCHA-equivalent challenge check. Anything but <see cref="Allowed"/> is a rejection.</summary>
public enum AbuseChallengeDecision
{
    /// <summary>Default value: deny. A zero-initialized or unset decision can never allow a submission (fail closed).</summary>
    Denied = 0,

    Allowed,
}

/// <summary>The material a verifier may inspect. Deliberately excludes the PII fields — a challenge vendor never needs the submitter's message.</summary>
public sealed record AbuseChallengeEvidence(string? ChallengeResponse);

/// <summary>
/// Port for the CAPTCHA-equivalent abuse control on public forms
/// (CC-CNT-004; SECURITY.md, Input validation rule 10). The mechanism is an
/// OPEN DECISION (issue 076, Open Questions): no provider is named anywhere
/// in the specs, and any third-party challenge script conflicts with the
/// third-party-runtime-CDN ban (SECURITY.md, Deployment rule 10) and the
/// strict CSP (HTTP boundary rule 2). Per CLAUDE.md, this module MUST NOT
/// silently pick a vendor — it ships only the enforcement hook, the
/// rejection path, and a fail-closed default. Implementations must throw or
/// return <see cref="AbuseChallengeDecision.Denied"/> on any doubt; the
/// endpoint treats an exception as a denial (SECURITY.md, Logging rule 2).
/// </summary>
public interface IAbuseChallengeVerifier
{
    ValueTask<AbuseChallengeDecision> VerifyAsync(AbuseChallengeEvidence evidence, CancellationToken cancellationToken);
}

/// <summary>
/// The provisional module default until a human ratifies a mechanism: denies
/// every submission. Fail closed is deliberate — the issue file does not
/// exempt launch from the control, so an unconfigured deployment refuses
/// contact submissions rather than running a public form with no
/// CAPTCHA-equivalent control (SECURITY.md, Input validation rule 10;
/// Logging rule 2). The host replaces this via DI once the decision lands.
/// </summary>
public sealed class UnconfiguredAbuseChallengeVerifier : IAbuseChallengeVerifier
{
    public ValueTask<AbuseChallengeDecision> VerifyAsync(AbuseChallengeEvidence evidence, CancellationToken cancellationToken) =>
        ValueTask.FromResult(AbuseChallengeDecision.Denied);
}
