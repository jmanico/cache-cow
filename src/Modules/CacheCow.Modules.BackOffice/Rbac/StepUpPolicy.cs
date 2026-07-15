namespace CacheCow.Modules.BackOffice.Rbac;

/// <summary>
/// Step-up re-authentication policy for sensitive dashboard actions —
/// refunds, employee-record access, role changes (SECURITY.md,
/// Authentication rule 2; CC-DSH-001).
///
/// What IS ratified: staff session lifetime max 12 hours, and that sensitive
/// actions require re-authentication (SECURITY.md, Authentication rule 2;
/// decision record 2026-07-15). What is NOT ratified anywhere is the step-up
/// max age — how recently that re-authentication must have happened. That
/// number is therefore REQUIRED configuration needing a human decision
/// (flagged; CLAUDE.md working rules): this type ships no default, and while
/// unconfigured every sensitive permission check denies
/// (<see cref="UnconfiguredStepUpPolicyProvider"/>).
/// </summary>
public sealed class StepUpPolicy
{
    /// <summary>
    /// The ratified maximum staff session lifetime (SECURITY.md,
    /// Authentication rule 2; ratified 2026-07-15). Session-lifetime
    /// enforcement itself is the session infrastructure's (issues 060/061);
    /// it bounds the step-up max age here because a re-authentication older
    /// than any possible session is meaningless.
    /// </summary>
    public static readonly TimeSpan RatifiedMaxStaffSessionLifetime = TimeSpan.FromHours(12);

    private StepUpPolicy(TimeSpan maxReauthAge)
    {
        MaxReauthAge = maxReauthAge;
    }

    /// <summary>
    /// How recently the staff member must have re-authenticated for a
    /// sensitive permission to be granted. Human-supplied configuration;
    /// no ratified default exists.
    /// </summary>
    public TimeSpan MaxReauthAge { get; }

    /// <summary>
    /// Validates the human-supplied step-up max age: positive and no longer
    /// than the ratified 12-hour session lifetime (SECURITY.md,
    /// Authentication rule 2). Out-of-bounds values are rejected at load,
    /// never clamped (SECURITY.md, Input validation rule 1).
    /// </summary>
    /// <exception cref="RbacConfigurationException">Non-positive or over-bound max age.</exception>
    public static StepUpPolicy Create(TimeSpan maxReauthAge)
    {
        if (maxReauthAge <= TimeSpan.Zero || maxReauthAge > RatifiedMaxStaffSessionLifetime)
        {
            throw new RbacConfigurationException(
                $"Step-up re-authentication max age must be positive and at most the ratified 12-hour staff session lifetime (SECURITY.md, Authentication rule 2); got {maxReauthAge}. Rejected at load.");
        }

        return new StepUpPolicy(maxReauthAge);
    }
}

/// <summary>
/// Source of the current step-up policy. The max-age number is required,
/// unratified configuration (see <see cref="StepUpPolicy"/>); null means
/// not configured, and sensitive permission checks then deny (fail closed).
/// </summary>
public interface IStepUpPolicyProvider
{
    /// <summary>The configured policy, or null when not configured.</summary>
    StepUpPolicy? Current { get; }
}

/// <summary>
/// Fail-closed default until a human decides the step-up max age (flagged
/// open configuration): no policy exists, so every sensitive permission
/// check denies. Non-sensitive permissions are unaffected.
/// </summary>
public sealed class UnconfiguredStepUpPolicyProvider : IStepUpPolicyProvider
{
    public StepUpPolicy? Current => null;
}

/// <summary>Serves one validated step-up policy, for host wiring.</summary>
public sealed class ConfiguredStepUpPolicyProvider : IStepUpPolicyProvider
{
    private readonly StepUpPolicy policy;

    public ConfiguredStepUpPolicyProvider(StepUpPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        this.policy = policy;
    }

    public StepUpPolicy? Current => policy;
}
