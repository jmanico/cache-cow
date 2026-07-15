using System.Text;
using CacheCow.Modules.ContentLocalization.Resources;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.ContentLocalization.Email;

/// <summary>
/// Composes order-confirmation and shipment-notification emails from
/// validated string resources (CC-ORD-007; issue 043). Template resolution is
/// whole-template: every key the kind needs must exist in one locale — the
/// requested locale, else the market's primary language — so a mixed-locale
/// or partially rendered message can never be produced (CC-I18N-006). The
/// composed body is plain text; every interpolated value is inserted as text
/// by the ICU pipeline (escape-by-default, SECURITY.md Input validation
/// rule 7). No address data can enter (see <see cref="OrderEmailSummary"/>)
/// and nothing but Content-Language enters headers (SECURITY.md, Email and
/// messaging security rule 1).
/// </summary>
public sealed class OrderEmailComposer
{
    private readonly StringResourceRegistry _registry;
    private readonly MarketPrimaryLocales _primaryLocales;

    public OrderEmailComposer(StringResourceRegistry registry, MarketPrimaryLocales primaryLocales)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(primaryLocales);
        _registry = registry;
        _primaryLocales = primaryLocales;
    }

    public ComposedOrderEmail Compose(OrderEmailKind kind, OrderEmailSummary summary, Locale requestedLocale, Market market)
    {
        ArgumentNullException.ThrowIfNull(summary);

        if (kind == OrderEmailKind.ShipmentNotification && summary.TrackingLink is null)
        {
            throw new ArgumentException("A shipment notification requires a tracking link (CC-ORD-008 consumer tracking).", nameof(summary));
        }

        if (kind == OrderEmailKind.OrderConfirmation && summary.TrackingLink is not null)
        {
            // Data minimization: a link (possibly capability-bearing, CC-ORD-010)
            // never rides along in a mail whose template does not need it.
            throw new ArgumentException("An order confirmation must not carry a tracking link (CC-ORD-007 data minimization).", nameof(summary));
        }

        var locale = ResolveTemplateLocale(kind, requestedLocale, market);

        var reference = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["orderReference"] = summary.OrderReference,
        };

        var subjectKey = kind == OrderEmailKind.OrderConfirmation
            ? OrderEmailResourceKeys.ConfirmationSubject
            : OrderEmailResourceKeys.ShipmentSubject;
        var introKey = kind == OrderEmailKind.OrderConfirmation
            ? OrderEmailResourceKeys.ConfirmationIntro
            : OrderEmailResourceKeys.ShipmentIntro;

        var subject = FormatKey(subjectKey, locale, reference);

        var body = new StringBuilder();
        body.Append(FormatKey(introKey, locale, reference));
        body.Append('\n');

        foreach (var line in summary.Lines)
        {
            body.Append('\n');
            body.Append(FormatKey(OrderEmailResourceKeys.Line, locale, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["itemName"] = line.ItemName,
                ["quantity"] = line.Quantity,
            }));
        }

        body.Append('\n').Append('\n');
        body.Append(FormatKey(OrderEmailResourceKeys.Total, locale, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["total"] = summary.TotalDisplay,
        }));

        if (kind == OrderEmailKind.ShipmentNotification)
        {
            body.Append('\n').Append('\n');
            body.Append(FormatKey(OrderEmailResourceKeys.ShipmentTracking, locale, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["trackingLink"] = summary.TrackingLink,
            }));
        }

        return new ComposedOrderEmail(kind, locale, subject, body.ToString());
    }

    /// <summary>
    /// Whole-template locale resolution (CC-I18N-006): the requested locale is
    /// used only when EVERY key of the template exists in it; otherwise the
    /// market's primary language is used, with the same all-keys requirement.
    /// No locale can satisfy the template → fail closed, send nothing.
    /// </summary>
    public Locale ResolveTemplateLocale(OrderEmailKind kind, Locale requestedLocale, Market market)
    {
        var keys = OrderEmailResourceKeys.ForKind(kind);

        if (keys.All(key => _registry.Contains(key, requestedLocale)))
        {
            return requestedLocale;
        }

        var primary = _primaryLocales.GetPrimaryLocale(market);
        if (keys.All(key => _registry.Contains(key, primary)))
        {
            return primary;
        }

        throw new MessageResourceMissingException(keys.First(key => !_registry.Contains(key, primary)), primary);
    }

    private string FormatKey(string key, Locale locale, IReadOnlyDictionary<string, object?> arguments)
    {
        if (!_registry.TryGetMessage(key, locale, out var message))
        {
            throw new MessageResourceMissingException(key, locale);
        }

        return message!.Format(locale, arguments);
    }
}
