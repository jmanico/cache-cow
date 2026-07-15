using CacheCow.SharedKernel;

namespace CacheCow.Modules.ContentLocalization.Email;

/// <summary>
/// Structurally complete PLACEHOLDER copy for the order-email templates in
/// all seven launch locales (CC-I18N-001, CC-I18N-006). FLAGGED: real copy is
/// a content task for native speakers per market (DESIGN.md §9 — per-market
/// native copy, plain verbs, no puns near money movement, DESIGN.md §5.4);
/// this set exists so the template registry, fallback, and CI key/placeholder
/// parity are testable end to end. It passes every issue 064 validation rule:
/// key parity, no HTML, ICU subset, placeholder parity.
/// </summary>
public static class PlaceholderOrderEmailResources
{
    /// <summary>The default resource set the module registers (replaceable by the host via <see cref="Resources.IStringResourceSource"/>).</summary>
    public static Resources.TranslationResourceSet Set { get; } = Build();

    private static Resources.TranslationResourceSet Build()
    {
        var resources = new Dictionary<Locale, IReadOnlyDictionary<string, string>>
        {
            [Locale.Parse("en-US")] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [OrderEmailResourceKeys.ConfirmationSubject] = "Cache Cow order {orderReference} confirmed",
                [OrderEmailResourceKeys.ConfirmationIntro] = "Thanks for your order {orderReference}. Here is what you ordered:",
                [OrderEmailResourceKeys.ShipmentSubject] = "Your Cache Cow order {orderReference} has shipped",
                [OrderEmailResourceKeys.ShipmentIntro] = "Good news — your order {orderReference} is on its way.",
                [OrderEmailResourceKeys.ShipmentTracking] = "Track your delivery: {trackingLink}",
                [OrderEmailResourceKeys.Line] = "{quantity, plural, one {# pack} other {# packs}} — {itemName}",
                [OrderEmailResourceKeys.Total] = "Order total: {total}",
            },
            [Locale.Parse("en-IN")] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [OrderEmailResourceKeys.ConfirmationSubject] = "Cache Cow order {orderReference} confirmed",
                [OrderEmailResourceKeys.ConfirmationIntro] = "Thanks for your order {orderReference}. Here is what you ordered:",
                [OrderEmailResourceKeys.ShipmentSubject] = "Your Cache Cow order {orderReference} has been dispatched",
                [OrderEmailResourceKeys.ShipmentIntro] = "Good news — your order {orderReference} is on its way.",
                [OrderEmailResourceKeys.ShipmentTracking] = "Track your delivery: {trackingLink}",
                [OrderEmailResourceKeys.Line] = "{quantity, plural, one {# pack} other {# packs}} — {itemName}",
                [OrderEmailResourceKeys.Total] = "Order total: {total}",
            },
            [Locale.Parse("es-ES")] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [OrderEmailResourceKeys.ConfirmationSubject] = "Pedido {orderReference} confirmado",
                [OrderEmailResourceKeys.ConfirmationIntro] = "Gracias por tu pedido {orderReference}. Esto es lo que has pedido:",
                [OrderEmailResourceKeys.ShipmentSubject] = "Tu pedido {orderReference} está en camino",
                [OrderEmailResourceKeys.ShipmentIntro] = "Buenas noticias: tu pedido {orderReference} ya está en camino.",
                [OrderEmailResourceKeys.ShipmentTracking] = "Sigue tu envío: {trackingLink}",
                [OrderEmailResourceKeys.Line] = "{quantity, plural, one {# paquete} other {# paquetes}} — {itemName}",
                [OrderEmailResourceKeys.Total] = "Total del pedido: {total}",
            },
            [Locale.Parse("es-MX")] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [OrderEmailResourceKeys.ConfirmationSubject] = "Pedido {orderReference} confirmado",
                [OrderEmailResourceKeys.ConfirmationIntro] = "Gracias por tu pedido {orderReference}. Esto es lo que pediste:",
                [OrderEmailResourceKeys.ShipmentSubject] = "Tu pedido {orderReference} va en camino",
                [OrderEmailResourceKeys.ShipmentIntro] = "Buenas noticias: tu pedido {orderReference} ya va en camino.",
                [OrderEmailResourceKeys.ShipmentTracking] = "Rastrea tu envío: {trackingLink}",
                [OrderEmailResourceKeys.Line] = "{quantity, plural, one {# paquete} other {# paquetes}} — {itemName}",
                [OrderEmailResourceKeys.Total] = "Total del pedido: {total}",
            },
            [Locale.Parse("de-DE")] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // Formal address (Sie) throughout commerce mail (DESIGN.md §8.4).
                [OrderEmailResourceKeys.ConfirmationSubject] = "Bestellung {orderReference} bestätigt",
                [OrderEmailResourceKeys.ConfirmationIntro] = "Vielen Dank für Ihre Bestellung {orderReference}. Ihre Artikel:",
                [OrderEmailResourceKeys.ShipmentSubject] = "Ihre Bestellung {orderReference} wurde versandt",
                [OrderEmailResourceKeys.ShipmentIntro] = "Gute Nachrichten: Ihre Bestellung {orderReference} ist unterwegs.",
                [OrderEmailResourceKeys.ShipmentTracking] = "Sendungsverfolgung: {trackingLink}",
                [OrderEmailResourceKeys.Line] = "{quantity, plural, one {# Paket} other {# Pakete}} — {itemName}",
                [OrderEmailResourceKeys.Total] = "Gesamtbetrag: {total}",
            },
            [Locale.Parse("ja-JP")] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [OrderEmailResourceKeys.ConfirmationSubject] = "ご注文 {orderReference} を承りました",
                [OrderEmailResourceKeys.ConfirmationIntro] = "ご注文 {orderReference} ありがとうございます。ご注文内容:",
                [OrderEmailResourceKeys.ShipmentSubject] = "ご注文 {orderReference} を発送しました",
                [OrderEmailResourceKeys.ShipmentIntro] = "ご注文 {orderReference} を発送いたしました。",
                [OrderEmailResourceKeys.ShipmentTracking] = "配送状況の確認: {trackingLink}",
                [OrderEmailResourceKeys.Line] = "{quantity, plural, other {#点}} — {itemName}",
                [OrderEmailResourceKeys.Total] = "合計: {total}",
            },
            [Locale.Parse("hi-IN")] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [OrderEmailResourceKeys.ConfirmationSubject] = "ऑर्डर {orderReference} की पुष्टि हो गई",
                [OrderEmailResourceKeys.ConfirmationIntro] = "आपके ऑर्डर {orderReference} के लिए धन्यवाद। आपके आइटम:",
                [OrderEmailResourceKeys.ShipmentSubject] = "आपका ऑर्डर {orderReference} भेज दिया गया है",
                [OrderEmailResourceKeys.ShipmentIntro] = "आपका ऑर्डर {orderReference} रास्ते में है।",
                [OrderEmailResourceKeys.ShipmentTracking] = "डिलीवरी ट्रैक करें: {trackingLink}",
                [OrderEmailResourceKeys.Line] = "{quantity, plural, one {# पैक} other {# पैक}} — {itemName}",
                [OrderEmailResourceKeys.Total] = "कुल राशि: {total}",
            },
        };

        return new Resources.TranslationResourceSet(resources);
    }
}
