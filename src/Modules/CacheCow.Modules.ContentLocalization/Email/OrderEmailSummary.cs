namespace CacheCow.Modules.ContentLocalization.Email;

/// <summary>The two transactional order emails of CC-ORD-007.</summary>
public enum OrderEmailKind
{
    OrderConfirmation,
    ShipmentNotification,
}

/// <summary>One order line as it appears in an email body: name and quantity only.</summary>
public sealed class OrderEmailLine
{
    public OrderEmailLine(string itemName, int quantity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemName);
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);
        OrderEmailSummary.RejectControlCharacters(itemName, nameof(itemName));
        ItemName = itemName;
        Quantity = quantity;
    }

    /// <summary>Localized display name of the item. Interpolated as plain text — hostile names render inert.</summary>
    public string ItemName { get; }

    public int Quantity { get; }
}

/// <summary>
/// The minimal typed input for composing an order email (CC-ORD-007: "no more
/// personal data than necessary"). Data minimization is enforced by the type
/// shape: there is deliberately NO address field of any kind — a full
/// delivery address in an email body is unrepresentable, not merely
/// forbidden. The tracking link is an opaque, pre-built HTTPS URL supplied by
/// the caller; whether it carries a guest capability token is the caller's
/// concern (CC-ORD-010/issue 042) — this type never places it anywhere but
/// the message body, never in headers or metadata (SECURITY.md, Email and
/// messaging security rule 1). The order total arrives pre-formatted
/// (locale-aware money formatting is CC-PRC-004, owned elsewhere).
/// </summary>
public sealed class OrderEmailSummary
{
    public OrderEmailSummary(
        string orderReference,
        IReadOnlyList<OrderEmailLine> lines,
        string totalDisplay,
        string? trackingLink = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderReference);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentException.ThrowIfNullOrWhiteSpace(totalDisplay);
        RejectControlCharacters(orderReference, nameof(orderReference));
        RejectControlCharacters(totalDisplay, nameof(totalDisplay));

        if (lines.Count == 0)
        {
            throw new ArgumentException("An order email needs at least one line.", nameof(lines));
        }

        if (lines.Any(l => l is null))
        {
            throw new ArgumentException("Order lines must not contain null entries.", nameof(lines));
        }

        if (trackingLink is not null)
        {
            RejectControlCharacters(trackingLink, nameof(trackingLink));
            if (!Uri.TryCreate(trackingLink, UriKind.Absolute, out var uri)
                || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "The tracking link must be an absolute https URL (SECURITY.md, Input validation rule 6).",
                    nameof(trackingLink));
            }
        }

        OrderReference = orderReference;
        Lines = lines.ToArray();
        TotalDisplay = totalDisplay;
        TrackingLink = trackingLink;
    }

    public string OrderReference { get; }

    public IReadOnlyList<OrderEmailLine> Lines { get; }

    /// <summary>Pre-formatted, locale-correct total (formatted upstream per CC-PRC-004); inserted as plain text.</summary>
    public string TotalDisplay { get; }

    /// <summary>Opaque pre-built HTTPS tracking URL; required for shipment notifications, forbidden otherwise.</summary>
    public string? TrackingLink { get; }

    /// <summary>
    /// No input reaches SMTP headers or splits lines in a composed message
    /// (SECURITY.md, Input validation rule 10 — email header injection).
    /// </summary>
    internal static void RejectControlCharacters(string value, string parameterName)
    {
        if (value.Any(char.IsControl))
        {
            throw new ArgumentException(
                "Control characters are not permitted in email content inputs (SECURITY.md, Input validation rule 10).",
                parameterName);
        }
    }
}
