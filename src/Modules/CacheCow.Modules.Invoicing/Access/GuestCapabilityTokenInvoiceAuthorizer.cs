using CacheCow.Modules.Invoicing.Invoices;

namespace CacheCow.Modules.Invoicing.Access;

/// <summary>
/// Guest-path authorizer (CC-INV-002; CC-ORD-010; CC-SEC-017): grants access
/// only when the presented capability token validates (unexpired, unrevoked —
/// via the issue-042 port) AND is bound to exactly the order this invoice was
/// issued for. Everything else — including any evaluation fault — is a denial
/// that maps to a uniform 404 (SECURITY.md, Authentication rules 9, 14;
/// Logging rule 2).
/// </summary>
public sealed class GuestCapabilityTokenInvoiceAuthorizer : IInvoiceAccessAuthorizer
{
    private readonly IGuestOrderCapabilityTokenValidator _tokenValidator;

    public GuestCapabilityTokenInvoiceAuthorizer(IGuestOrderCapabilityTokenValidator tokenValidator)
    {
        ArgumentNullException.ThrowIfNull(tokenValidator);
        _tokenValidator = tokenValidator;
    }

    public InvoiceAccessDecision Authorize(Invoice invoice, InvoiceAccessRequest request)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(invoice);

            if (request is not GuestTokenAccessRequest guestRequest)
            {
                return InvoiceAccessDecision.Denied(InvoiceAccessDenialReason.UnsupportedRequestKind);
            }

            var validation = _tokenValidator.Validate(guestRequest.PresentedToken);
            if (validation is null || !validation.IsValid || validation.BoundOrder is not { } boundOrder)
            {
                return InvoiceAccessDecision.Denied(InvoiceAccessDenialReason.CapabilityTokenInvalid);
            }

            return boundOrder.Equals(invoice.Order)
                ? InvoiceAccessDecision.Granted()
                : InvoiceAccessDecision.Denied(InvoiceAccessDenialReason.CapabilityTokenBoundToOtherOrder);
        }
#pragma warning disable CA1031 // Fail closed: ANY fault in an authorization path is a denial, never a bypass (SECURITY.md, Logging rule 2).
        catch (Exception)
#pragma warning restore CA1031
        {
            return InvoiceAccessDecision.Denied(InvoiceAccessDenialReason.EvaluationFault);
        }
    }
}
