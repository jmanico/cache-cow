using CacheCow.SharedKernel;

namespace CacheCow.Modules.ContentLocalization.Email;

/// <summary>
/// Composes and dispatches an order email (CC-ORD-007). Which order state
/// transitions trigger which send lives with the Ordering context (CC-ORD-006,
/// issue 035) and reaches this service through the module boundary. The
/// recipient address goes only to the dispatch port — user input never
/// reaches SMTP headers (SECURITY.md, Input validation rule 10).
/// </summary>
public sealed class OrderEmailService
{
    private readonly OrderEmailComposer _composer;
    private readonly IEmailDispatch _dispatch;

    public OrderEmailService(OrderEmailComposer composer, IEmailDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(composer);
        ArgumentNullException.ThrowIfNull(dispatch);
        _composer = composer;
        _dispatch = dispatch;
    }

    public async Task<ComposedOrderEmail> SendAsync(
        OrderEmailKind kind,
        OrderEmailSummary summary,
        string recipientEmailAddress,
        Locale requestedLocale,
        Market market,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientEmailAddress);
        OrderEmailSummary.RejectControlCharacters(recipientEmailAddress, nameof(recipientEmailAddress));

        var composed = _composer.Compose(kind, summary, requestedLocale, market);
        await _dispatch.DispatchAsync(composed, recipientEmailAddress, cancellationToken).ConfigureAwait(false);
        return composed;
    }
}
