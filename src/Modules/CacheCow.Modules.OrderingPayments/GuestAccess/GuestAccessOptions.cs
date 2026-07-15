namespace CacheCow.Modules.OrderingPayments.GuestAccess;

/// <summary>
/// Required guest-access configuration. OPEN DECISION: CC-ORD-010 mandates
/// that capability tokens expire, but no lifetime duration is ratified
/// anywhere in the specs (nor whether expiry should track the order
/// lifecycle, e.g. delivery + N days for invoice access — issue 042, Open
/// Questions). This type therefore has NO default and no parameterless
/// constructor; the host supplies the ratified value.
/// </summary>
public sealed class GuestAccessOptions
{
    /// <param name="tokenLifetime">Lifetime of an issued capability token; must be positive.</param>
    public GuestAccessOptions(TimeSpan tokenLifetime)
    {
        if (tokenLifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tokenLifetime),
                tokenLifetime,
                "The capability-token lifetime must be positive (CC-ORD-010; duration awaits human ratification).");
        }

        TokenLifetime = tokenLifetime;
    }

    public TimeSpan TokenLifetime { get; }
}
