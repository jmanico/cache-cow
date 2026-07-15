using CacheCow.Modules.Invoicing.Invoices;

namespace CacheCow.Modules.Invoicing.Access;

/// <summary>
/// A request to access an issued invoice (download, status). Exactly two
/// closed kinds exist (CC-INV-002): guest capability token (CC-ORD-010) and
/// authenticated account session (SECURITY.md, Authentication rules 9, 14).
/// Order-number-plus-email is deliberately unrepresentable (CC-ORD-010).
/// </summary>
public abstract class InvoiceAccessRequest
{
    private protected InvoiceAccessRequest()
    {
    }
}

/// <summary>
/// Guest access: the presented CC-ORD-010 capability token — the sole
/// credential; validated per request, never trusted from possession alone.
/// </summary>
public sealed class GuestTokenAccessRequest : InvoiceAccessRequest
{
    public GuestTokenAccessRequest(string presentedToken)
    {
        // Empty/whitespace is representable and simply never authorizes:
        // malformed input is a denial, not an exception path (fail closed).
        PresentedToken = presentedToken ?? string.Empty;
    }

    /// <summary>The raw presented token. A secret — see <see cref="ToString"/>.</summary>
    public string PresentedToken { get; }

    /// <summary>Redacted — capability tokens never reach logs or telemetry (SECURITY.md, Logging rule 4; issue 048, AC-07).</summary>
    public override string ToString() => "GuestTokenAccessRequest[redacted]";
}

/// <summary>
/// Account-holder access: the authenticated session's account identity,
/// established upstream by Identity &amp; Access — never client-asserted.
/// </summary>
public sealed class AccountSessionAccessRequest : InvoiceAccessRequest
{
    public AccountSessionAccessRequest(AccountReference account)
    {
        _ = account.Value; // uninitialized reference fails closed
        Account = account;
    }

    public AccountReference Account { get; }
}
