using System.Reflection;
using System.Text;
using CacheCow.Modules.Invoicing.Invoices;
using CacheCow.Modules.Invoicing.Rendering;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.Invoicing.Tests;

/// <summary>
/// Issue 048, AC-01: the PDF renderer port consumes ONLY the structured
/// invoice aggregate plus locale — no free-text, HTML, or CMS channel exists
/// on the contract. The renderer implementation/library is an open decision
/// (issue 048, Open Questions); these tests pin the port contract with a test
/// double, not a library.
/// </summary>
public sealed class PdfRenderingPortTests
{
    /// <summary>
    /// Test double proving the contract is satisfiable from structured fields
    /// alone: it renders from the aggregate it receives and records what
    /// reached it.
    /// </summary>
    private sealed class RecordingPdfRenderer : IInvoicePdfRenderer
    {
        public InvoicePdfRenderRequest? LastRequest { get; private set; }

        public InvoicePdfDocument Render(InvoicePdfRenderRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            LastRequest = request;

            // Structured fields only — no other content source is reachable.
            var structuredOnly = new StringBuilder()
                .Append(request.Invoice.Number).Append('\n')
                .Append(request.Invoice.Market.Code).Append('\n')
                .Append(request.Locale.Tag).Append('\n')
                .Append(request.Invoice.Total).Append('\n');
            foreach (var line in request.Invoice.Lines)
            {
                structuredOnly.Append(line.LegalDescription).Append('\t').Append(line.LineTotal).Append('\n');
            }

            return new InvoicePdfDocument(Encoding.UTF8.GetBytes(structuredOnly.ToString()));
        }
    }

    [Fact]
    [Requirement("CC-INV-002")]
    public void Renderer_port_accepts_only_the_structured_aggregate_and_locale()
    {
        // The port has exactly one method with exactly one parameter: the
        // structured render request. No overload takes HTML, template text,
        // CMS content, or a stream (issue 048, AC-01, Anti-Patterns).
        var methods = typeof(IInvoicePdfRenderer).GetMethods();
        var render = Assert.Single(methods);
        var parameter = Assert.Single(render.GetParameters());
        Assert.Equal(typeof(InvoicePdfRenderRequest), parameter.ParameterType);

        // And the request itself exposes exactly two content channels:
        // the immutable Invoice aggregate and the rendering Locale.
        var properties = typeof(InvoicePdfRenderRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => (property.Name, property.PropertyType))
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [("Invoice", typeof(Invoice)), ("Locale", typeof(Locale))],
            properties);
    }

    [Fact]
    [Requirement("CC-INV-002")]
    public void Render_request_offers_no_free_text_or_markup_channel()
    {
        // No string/stream/byte constructor parameter anywhere on the request:
        // the only way to influence the document is the structured aggregate.
        var constructorParameters = typeof(InvoicePdfRenderRequest)
            .GetConstructors()
            .SelectMany(ctor => ctor.GetParameters())
            .ToArray();

        Assert.All(constructorParameters, parameter => Assert.True(
            parameter.ParameterType == typeof(Invoice) || parameter.ParameterType == typeof(Locale),
            $"Unexpected render input channel '{parameter.Name}' ({parameter.ParameterType.Name}) — "
            + "PDFs render from structured invoice data only (CC-INV-002)."));
    }

    [Fact]
    [Requirement("CC-INV-002")]
    public void Renderer_renders_from_the_structured_aggregate()
    {
        var issuer = InvoiceFixtures.NewIssuer();
        var invoice = issuer.Issue(InvoiceFixtures.Draft(Market.JP));
        var renderer = new RecordingPdfRenderer();

        var document = renderer.Render(new InvoicePdfRenderRequest(invoice, Locale.Parse("ja-JP")));

        Assert.Same(invoice, renderer.LastRequest!.Invoice);
        Assert.False(document.Content.IsEmpty);
        Assert.Equal("application/pdf", InvoicePdfDocument.MediaType);
    }

    [Fact]
    [Requirement("CC-INV-002")]
    public void Empty_render_output_fails_closed()
    {
        // A rendering failure never yields a partial/empty document (issue 048,
        // Failure Behavior).
        Assert.ThrowsAny<InvalidOperationException>(() => new InvoicePdfDocument(ReadOnlyMemory<byte>.Empty));
    }
}
