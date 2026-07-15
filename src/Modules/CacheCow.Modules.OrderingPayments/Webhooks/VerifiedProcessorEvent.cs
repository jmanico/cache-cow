namespace CacheCow.Modules.OrderingPayments.Webhooks;

/// <summary>
/// A processor callback whose sender signature was verified over the raw
/// request body, whose timestamp is within the replay bound, and whose event
/// id was not seen before (issue 041; CC-SEC-014). The constructor is
/// internal and the only producer is <see cref="WebhookVerifier"/> — an
/// unverified payload is unrepresentable downstream, by construction.
///
/// Downstream per-processor parsing (issues 039/040) reads
/// <see cref="RawBody"/> from here, so parsing can only ever happen AFTER
/// verification (SECURITY.md, Input validation rule 11). A verified event is
/// still not sufficient authority to move money or order state on its own:
/// see <see cref="Payments.PaymentAuthorityService"/> (CC-ORD-009).
/// </summary>
public sealed class VerifiedProcessorEvent
{
    internal VerifiedProcessorEvent(
        string processorName,
        string eventId,
        DateTimeOffset timestamp,
        ReadOnlyMemory<byte> rawBody,
        DateTimeOffset verifiedAt)
    {
        ProcessorName = processorName;
        EventId = eventId;
        Timestamp = timestamp;
        RawBody = rawBody;
        VerifiedAt = verifiedAt;
    }

    public string ProcessorName { get; }

    public string EventId { get; }

    /// <summary>Sender-claimed timestamp, already checked against the replay bound.</summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>The signature-covered raw body, for post-verification parsing (issues 039/040).</summary>
    public ReadOnlyMemory<byte> RawBody { get; }

    /// <summary>Server clock at verification.</summary>
    public DateTimeOffset VerifiedAt { get; }
}
