namespace CacheCow.Modules.OrderingPayments.Webhooks;

/// <summary>
/// One inbound processor callback exactly as it crossed the untrusted webhook
/// boundary (issue 041; ARCHITECTURE.md, Server bounded contexts 4): the RAW
/// request body bytes — captured before any parsing or model binding — plus
/// transport metadata. Everything here is attacker-controlled until
/// <see cref="WebhookVerifier"/> verifies it (SECURITY.md, Input validation
/// rule 11).
///
/// The processor-specific wire mapping (which header carries the signature,
/// how Stripe/Razorpay encode timestamp and event id) belongs to the
/// per-processor adapters (issues 039/040); this type is processor-agnostic.
/// There is deliberately NO type in this module representing a browser
/// redirect return from a payment flow — redirect-derived "success" input is
/// unrepresentable, not merely ignored (CC-ORD-009).
///
/// No ToString override exists: attacker-controlled values never ride into
/// logs by accidental formatting (SECURITY.md, Logging rule 5).
/// </summary>
public sealed class UnverifiedWebhookDelivery
{
    /// <param name="processorName">Server-derived sender identity (which receiver endpoint got the call), not a claim from the payload.</param>
    /// <param name="rawBody">The raw, unparsed request body bytes the signature must cover.</param>
    /// <param name="signatureHex">Hex-encoded HMAC-SHA256 signature as presented by the sender; null/blank when absent.</param>
    /// <param name="timestamp">Sender-claimed delivery timestamp from transport metadata; null when absent.</param>
    /// <param name="eventId">Sender-claimed unique event id (replay nonce) from transport metadata; null when absent.</param>
    public UnverifiedWebhookDelivery(
        string processorName,
        ReadOnlyMemory<byte> rawBody,
        string? signatureHex,
        DateTimeOffset? timestamp,
        string? eventId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processorName);
        ProcessorName = processorName;
        RawBody = rawBody;
        SignatureHex = signatureHex;
        Timestamp = timestamp;
        EventId = eventId;
    }

    public string ProcessorName { get; }

    public ReadOnlyMemory<byte> RawBody { get; }

    public string? SignatureHex { get; }

    public DateTimeOffset? Timestamp { get; }

    public string? EventId { get; }
}
