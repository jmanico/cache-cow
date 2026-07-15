namespace CacheCow.Modules.OrderingPayments.GuestAccess;

/// <summary>
/// The single purpose a guest capability token is bound to (issue 042;
/// CC-ORD-010: single-purpose tokens). A token issued for one purpose grants
/// nothing for any other, exactly as it grants nothing for any other order.
/// </summary>
public enum GuestAccessPurpose
{
    /// <summary>Guest order-status view.</summary>
    OrderStatus = 0,

    /// <summary>Guest shipment tracking (CC-ORD-008 surface).</summary>
    OrderTracking = 1,

    /// <summary>Guest invoice download link (CC-INV-002: the link resolves via this token, never a guessable order identifier).</summary>
    InvoiceDownload = 2,
}
