using CacheCow.Modules.Invoicing.Invoices;

namespace CacheCow.Modules.Invoicing.Access;

/// <summary>
/// Account-path authorizer: server-side object-level authorization scoping
/// invoice access to the owning account (SECURITY.md, Authentication rule 9;
/// CC-SEC-007). A guest invoice has no owning account and is therefore never
/// reachable through a session — only through its capability token. Any
/// mismatch or fault is a denial mapping to a uniform 404.
/// </summary>
public sealed class AccountSessionInvoiceAuthorizer : IInvoiceAccessAuthorizer
{
    public InvoiceAccessDecision Authorize(Invoice invoice, InvoiceAccessRequest request)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(invoice);

            if (request is not AccountSessionAccessRequest sessionRequest)
            {
                return InvoiceAccessDecision.Denied(InvoiceAccessDenialReason.UnsupportedRequestKind);
            }

            return invoice.CustomerAccount is { } owner && owner.Equals(sessionRequest.Account)
                ? InvoiceAccessDecision.Granted()
                : InvoiceAccessDecision.Denied(InvoiceAccessDenialReason.NotResourceOwner);
        }
#pragma warning disable CA1031 // Fail closed: ANY fault in an authorization path is a denial, never a bypass (SECURITY.md, Logging rule 2).
        catch (Exception)
#pragma warning restore CA1031
        {
            return InvoiceAccessDecision.Denied(InvoiceAccessDenialReason.EvaluationFault);
        }
    }
}
