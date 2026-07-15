namespace CacheCow.Modules.Invoicing.Rendering;

/// <summary>
/// Port: server-side invoice PDF rendering from structured invoice data only
/// (CC-INV-002; ARCHITECTURE.md, "Server bounded contexts" item 7:
/// "server-rendered PDFs behind authenticated download").
///
/// Contract:
/// <list type="bullet">
/// <item>The renderer consumes ONLY <see cref="InvoicePdfRenderRequest"/> —
/// the structured aggregate plus locale. No CMS content, free text, or
/// client-supplied data may reach the document (issue 048, AC-01,
/// Anti-Patterns).</item>
/// <item>Amounts are locale-formatted (JPY zero-decimal, INR lakh/crore
/// grouping — CC-PRC-004; DESIGN.md §4.4); zero puns in financial content
/// (DESIGN.md §5.4).</item>
/// <item>Failures throw; a partial document or another order's data is never
/// returned (issue 048, Failure Behavior — fail closed).</item>
/// </list>
///
/// OPEN DECISION — no adapter is provided and no PDF library is chosen here:
/// the rendering mechanism/library is unspecified (issue 048, Open Questions)
/// and any candidate must clear SECURITY.md's Dependency Rules 1–8. The host
/// supplies the adapter once a human decides.
/// </summary>
public interface IInvoicePdfRenderer
{
    InvoicePdfDocument Render(InvoicePdfRenderRequest request);
}
