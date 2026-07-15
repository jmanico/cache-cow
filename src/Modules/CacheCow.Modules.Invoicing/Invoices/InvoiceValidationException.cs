namespace CacheCow.Modules.Invoicing.Invoices;

/// <summary>
/// An invoice or credit note failed domain validation and was NOT issued.
/// Issuance fails closed: no number is allocated and no record exists for a
/// draft that does not validate (issue 046, Failure Behavior). Messages here
/// are internal; API surfaces translate to RFC 9457 problem details without
/// internal state (SECURITY.md, Logging rule 1 — issue 021).
/// </summary>
public sealed class InvoiceValidationException : Exception
{
    public InvoiceValidationException(string message)
        : base(message)
    {
    }
}
