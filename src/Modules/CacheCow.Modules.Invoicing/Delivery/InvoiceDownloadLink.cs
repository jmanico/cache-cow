using CacheCow.Modules.Invoicing.Invoices;

namespace CacheCow.Modules.Invoicing.Delivery;

/// <summary>
/// The access key a consumer invoice email links to — opaque by construction
/// (CC-INV-002; CC-ORD-010). The type has exactly two factories and neither
/// accepts an invoice number, order number, or any other guessable/enumerable
/// identifier (issue 048, AC-06):
/// <list type="bullet">
/// <item><see cref="FromCapabilityToken"/> — guest orders: the link value IS
/// the CC-ORD-010 per-order capability token (unguessable, expiring,
/// server-revocable; minted by Ordering &amp; Payments, issue 042). Possession
/// confers nothing: the download endpoint re-validates the token per request
/// (SECURITY.md, Authentication rule 14).</item>
/// <item><see cref="ForAccountHolder"/> — account holders: the link carries
/// the invoice's random 128-bit <see cref="InvoiceId"/>; access resolves only
/// through session identity plus object-level authorization (SECURITY.md,
/// Authentication rule 9).</item>
/// </list>
/// The key is a secret: HTTPS-only, never logged (<see cref="ToString"/>
/// redacts), kept out of Referer and analytics query strings (SECURITY.md,
/// Authentication rule 14; issue 048, AC-07). URL shape is a host concern;
/// this type carries only the opaque key.
/// </summary>
public sealed class InvoiceDownloadLink
{
    /// <summary>128 bits base64url-encoded is 22 characters — the CC-ORD-010 entropy floor.</summary>
    public const int MinimumKeyLength = 22;

    private InvoiceDownloadLink(string opaqueAccessKey)
    {
        OpaqueAccessKey = opaqueAccessKey;
    }

    /// <summary>The opaque access key. Contains no invoice/order number by construction.</summary>
    public string OpaqueAccessKey { get; }

    /// <summary>
    /// Guest path: derive the link from the CC-ORD-010 capability token — the
    /// sole guest access credential. The token implementation lives in
    /// Ordering &amp; Payments (issue 042); this factory only rejects values
    /// that are structurally guessable (too short for 128 bits of entropy, or
    /// purely numeric like an order number) instead of re-implementing it.
    /// </summary>
    public static InvoiceDownloadLink FromCapabilityToken(string capabilityToken)
    {
        if (string.IsNullOrWhiteSpace(capabilityToken)
            || capabilityToken.Length < MinimumKeyLength
            || capabilityToken.All(char.IsAsciiDigit))
        {
            throw new InvalidOperationException(
                "A guest invoice link derives only from an unguessable CC-ORD-010 capability token — "
                + "never from a guessable or enumerable identifier (CC-INV-002; issue 048, AC-06).");
        }

        return new InvoiceDownloadLink(capabilityToken);
    }

    /// <summary>
    /// Account-holder path: the opaque random invoice identity; the download
    /// endpoint still enforces session + object-level authorization per
    /// request (SECURITY.md, Authentication rule 9).
    /// </summary>
    public static InvoiceDownloadLink ForAccountHolder(InvoiceId invoiceId) =>
        new(invoiceId.Value);

    /// <summary>Redacted — access keys never reach logs or telemetry (SECURITY.md, Logging rule 4).</summary>
    public override string ToString() => "InvoiceDownloadLink[redacted]";
}
