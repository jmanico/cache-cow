namespace CacheCow.Modules.OrderingPayments.Idempotency;

/// <summary>
/// Required idempotency configuration. OPEN DECISION: the retention window
/// duration is specified nowhere (CC-API-005 says "within the retention
/// window" without a number) and interacts with the CC-CMP-003 retention
/// schedule — it needs human ratification, so this type has NO default and no
/// parameterless constructor (issue 037, Open Questions).
/// </summary>
public sealed class IdempotencyOptions
{
    /// <param name="retentionWindow">How long a completed (scope, key) entry replays its stored result; must be positive.</param>
    public IdempotencyOptions(TimeSpan retentionWindow)
    {
        if (retentionWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retentionWindow),
                retentionWindow,
                "The idempotency retention window must be positive (CC-API-005; value awaits human ratification).");
        }

        RetentionWindow = retentionWindow;
    }

    public TimeSpan RetentionWindow { get; }
}
