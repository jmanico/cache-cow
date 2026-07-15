namespace CacheCow.Modules.ContentLocalization.Email;

/// <summary>
/// The resource keys each transactional email template references (issue 043:
/// templates are built on the issue 064 resource pipeline, CC-I18N-002). A
/// template renders as a whole in exactly one locale — key sets exist so the
/// composer can verify full-template availability before rendering anything
/// (CC-I18N-006: never a broken or partially-localized template).
/// </summary>
public static class OrderEmailResourceKeys
{
    public const string ConfirmationSubject = "email.order-confirmation.subject";
    public const string ConfirmationIntro = "email.order-confirmation.intro";
    public const string ShipmentSubject = "email.shipment-notification.subject";
    public const string ShipmentIntro = "email.shipment-notification.intro";
    public const string ShipmentTracking = "email.shipment-notification.tracking";
    public const string Line = "email.order.line";
    public const string Total = "email.order.total";

    /// <summary>Every key the given email kind renders; the whole set must resolve in one locale.</summary>
    public static IReadOnlyList<string> ForKind(OrderEmailKind kind) => kind switch
    {
        OrderEmailKind.OrderConfirmation => [ConfirmationSubject, ConfirmationIntro, Line, Total],
        OrderEmailKind.ShipmentNotification => [ShipmentSubject, ShipmentIntro, Line, Total, ShipmentTracking],
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown email kind."),
    };
}
