namespace CacheCow.Modules.Invoicing.Rendering;

/// <summary>
/// A rendered invoice PDF. Served only through the authenticated download
/// endpoint over HTTPS with <c>Cache-Control: no-store</c> and
/// <c>X-Content-Type-Options: nosniff</c> (SECURITY.md, HTTP boundary
/// rules 1, 3, 10) — never attached to email (CC-INV-002).
/// </summary>
public sealed class InvoicePdfDocument
{
    public InvoicePdfDocument(ReadOnlyMemory<byte> content)
    {
        if (content.IsEmpty)
        {
            throw new InvalidOperationException(
                "A rendering failure returns an error, never an empty or partial document (issue 048, Failure Behavior).");
        }

        Content = content;
    }

    public ReadOnlyMemory<byte> Content { get; }

    /// <summary>Always application/pdf; the download endpoint pairs it with nosniff (SECURITY.md, HTTP boundary rule 3).</summary>
    public static string MediaType => "application/pdf";
}
