using CacheCow.SharedKernel;

namespace CacheCow.Modules.Invoicing.Delivery;

/// <summary>
/// The delivery model for a consumer invoice email: a download LINK only
/// (ratified 2026-07-15 — CC-INV-002). By construction this type has no
/// attachment representation of any kind and carries no postal-address or
/// other order PII (CC-ORD-007: no more personal data than necessary, no full
/// address in email body). Template text per locale is the transactional email
/// pipeline's concern (issue 043; CC-I18N-006); this model carries only what
/// the Invoicing context owns.
/// </summary>
public sealed class InvoiceEmailMessage
{
    public InvoiceEmailMessage(string recipientEmailAddress, Locale locale, InvoiceDownloadLink downloadLink)
    {
        ArgumentNullException.ThrowIfNull(downloadLink);
        if (string.IsNullOrWhiteSpace(recipientEmailAddress))
        {
            throw new InvalidOperationException("An invoice email requires a recipient address.");
        }

        _ = locale.Tag; // uninitialized Locale fails closed

        RecipientEmailAddress = recipientEmailAddress;
        Locale = locale;
        DownloadLink = downloadLink;
    }

    public string RecipientEmailAddress { get; }

    /// <summary>Template locale; fallback is the market's primary language (CC-I18N-006).</summary>
    public Locale Locale { get; }

    /// <summary>The ONLY invoice-content channel in the email (CC-INV-002).</summary>
    public InvoiceDownloadLink DownloadLink { get; }
}
