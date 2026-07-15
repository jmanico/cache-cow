using CacheCow.Modules.Invoicing.Invoices;

namespace CacheCow.Modules.Invoicing.Access;

/// <summary>
/// Port: per-request authorization for invoice access (download, status).
/// Two implementations-by-contract exist — account-session object-level
/// authorization (<see cref="AccountSessionInvoiceAuthorizer"/>, SECURITY.md
/// Authentication rule 9) and guest capability-token validation
/// (<see cref="GuestCapabilityTokenInvoiceAuthorizer"/>, rule 14; CC-ORD-010).
///
/// Contract:
/// <list type="bullet">
/// <item>Fail closed: any fault during evaluation is a denial, never a bypass
/// (SECURITY.md, Logging rule 2).</item>
/// <item>A denial maps to a uniform HTTP 404 — never 403, never anything that
/// confirms the invoice exists (SECURITY.md, Authentication rule 9; issue 048,
/// AC-04/AC-06).</item>
/// <item>Possession of a link confers nothing; every call re-validates
/// (issue 048, Zero Trust Consideration).</item>
/// </list>
/// </summary>
public interface IInvoiceAccessAuthorizer
{
    InvoiceAccessDecision Authorize(Invoice invoice, InvoiceAccessRequest request);
}
