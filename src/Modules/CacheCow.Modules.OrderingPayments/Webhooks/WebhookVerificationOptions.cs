namespace CacheCow.Modules.OrderingPayments.Webhooks;

/// <summary>
/// Required webhook-verification configuration. OPEN DECISION: the specs
/// mandate "timestamp/nonce replay bounds" (CC-SEC-014) but set no window
/// duration and no seen-nonce retention period (issue 041, Open Questions) —
/// so this type has NO default and no parameterless constructor; the host
/// must supply the ratified value.
/// </summary>
public sealed class WebhookVerificationOptions
{
    /// <param name="maxEventAge">
    /// Maximum acceptable distance (in either direction) between the server
    /// clock and a delivery's sender-claimed timestamp; must be positive.
    /// </param>
    public WebhookVerificationOptions(TimeSpan maxEventAge)
    {
        if (maxEventAge <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxEventAge),
                maxEventAge,
                "The webhook max event age must be positive (CC-SEC-014; value awaits human ratification).");
        }

        MaxEventAge = maxEventAge;
    }

    public TimeSpan MaxEventAge { get; }
}
