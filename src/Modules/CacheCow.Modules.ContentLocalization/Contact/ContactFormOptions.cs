namespace CacheCow.Modules.ContentLocalization.Contact;

/// <summary>
/// Host-supplied configuration for the contact endpoint. Deliberately NOT
/// registered with a default by the module: the destination of accepted
/// submissions is an open product decision (issue 076, Open Questions — an
/// operations mailbox is the provisional shape this type models), and the
/// minimum-fill-time threshold is an unratified number. Until the host
/// registers an instance, the endpoint fails closed with 503 and processes
/// nothing (SECURITY.md, Logging rule 2).
/// </summary>
public sealed class ContactFormOptions
{
    public ContactFormOptions(string internalRecipientEmailAddress, long minimumFillMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(internalRecipientEmailAddress);
        if (!ContactSubmission.IsValidEmailAddressSyntax(internalRecipientEmailAddress))
        {
            throw new ArgumentException(
                "The internal recipient must be a syntactically valid email address (CC-CNT-004).",
                nameof(internalRecipientEmailAddress));
        }

        if (minimumFillMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumFillMilliseconds),
                "The minimum fill time must be positive; the heuristic cannot be configured off (SECURITY.md, Input validation rule 10).");
        }

        InternalRecipientEmailAddress = internalRecipientEmailAddress;
        MinimumFillMilliseconds = minimumFillMilliseconds;
    }

    /// <summary>Where accepted submissions are forwarded. Server configuration — the ONLY recipient the endpoint ever dispatches to; never user-influenced.</summary>
    public string InternalRecipientEmailAddress { get; }

    /// <summary>Submissions reporting a fill time below this are rejected as automated. Integer milliseconds (no binary floating point anywhere, CC-PRC-003 discipline).</summary>
    public long MinimumFillMilliseconds { get; }
}
