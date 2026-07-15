using System.Text;
using System.Text.Encodings.Web;

namespace CacheCow.Modules.ContentLocalization.Rendering;

/// <summary>
/// The sanitizing allowlist renderer: the ONLY path from CMS rich text to
/// HTML (CC-SEC-002, CC-CNT-001; SECURITY.md, Input validation rule 5;
/// ARCHITECTURE.md, bounded context 10). It walks the typed node tree and
/// emits encoded markup: every text node is HTML-encoded, only the explicit
/// node allowlist renders, hyperlink destinations pass the URL policy
/// (scheme allowlist with '#' fallback, CC-SEC-004), and any node outside the
/// allowlist rejects the whole document — fail closed, never emit raw content
/// as a fallback (SECURITY.md, Logging rule 2; issue 072 failure behavior).
/// There is no configuration that widens the allowlist and no raw-HTML input
/// anywhere in the node model.
/// </summary>
public sealed class AllowlistRichTextRenderer
{
    private const int MaxDepth = 32;

    private static readonly HtmlEncoder Encoder = HtmlEncoder.Default;

    private readonly IHyperlinkUrlPolicy _urlPolicy;

    public AllowlistRichTextRenderer(IHyperlinkUrlPolicy urlPolicy)
    {
        ArgumentNullException.ThrowIfNull(urlPolicy);
        _urlPolicy = urlPolicy;
    }

    /// <summary>
    /// Renders the document to encoded HTML, or throws
    /// <see cref="DisallowedRichTextNodeException"/> when any node falls
    /// outside the allowlist — the document is rejected as a whole.
    /// </summary>
    public string Render(RichTextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var builder = new StringBuilder();
        RenderChildren(builder, document.Children, depth: 0);
        return builder.ToString();
    }

    private void RenderChildren(StringBuilder builder, IReadOnlyList<RichTextNode> children, int depth)
    {
        foreach (var child in children)
        {
            RenderNode(builder, child, depth);
        }
    }

    private void RenderNode(StringBuilder builder, RichTextNode node, int depth)
    {
        if (depth >= MaxDepth)
        {
            throw new DisallowedRichTextNodeException(
                $"Rich-text tree exceeds the maximum depth of {MaxDepth}; rejecting the document (fail closed).");
        }

        switch (node)
        {
            case RichTextTextNode text:
                builder.Append(Encoder.Encode(text.Text));
                break;

            case RichTextParagraphNode paragraph:
                RenderElement(builder, "p", paragraph.Children, depth);
                break;

            case RichTextHeadingNode heading:
                RenderElement(builder, "h" + heading.Level.ToString(System.Globalization.CultureInfo.InvariantCulture), heading.Children, depth);
                break;

            case RichTextBoldNode bold:
                RenderElement(builder, "strong", bold.Children, depth);
                break;

            case RichTextItalicNode italic:
                RenderElement(builder, "em", italic.Children, depth);
                break;

            case RichTextListNode list:
            {
                var tag = list.Ordered ? "ol" : "ul";
                builder.Append('<').Append(tag).Append('>');
                foreach (var item in list.Items)
                {
                    RenderNode(builder, item, depth + 1);
                }

                builder.Append("</").Append(tag).Append('>');
                break;
            }

            case RichTextListItemNode item:
                RenderElement(builder, "li", item.Children, depth);
                break;

            case RichTextHyperlinkNode hyperlink:
            {
                // Untrusted destination: scheme-allowlisted with '#' fallback
                // (CC-SEC-004), then attribute-encoded.
                var safeHref = _urlPolicy.Resolve(hyperlink.Destination);
                builder.Append("<a href=\"").Append(Encoder.Encode(safeHref)).Append("\">");
                RenderChildren(builder, hyperlink.Children, depth + 1);
                builder.Append("</a>");
                break;
            }

            default:
                // Not on the allowlist: reject the document, never skip or
                // pass through (CC-SEC-002; SECURITY.md, Input validation
                // rule 1 — reject, don't sanitize into acceptance).
                throw new DisallowedRichTextNodeException(
                    $"Node type '{node.GetType().Name}' is not on the render allowlist; rejecting the document (CC-SEC-002).");
        }
    }

    private void RenderElement(StringBuilder builder, string tag, IReadOnlyList<RichTextNode> children, int depth)
    {
        builder.Append('<').Append(tag).Append('>');
        RenderChildren(builder, children, depth + 1);
        builder.Append("</").Append(tag).Append('>');
    }
}

/// <summary>
/// A rich-text document contained a node outside the render allowlist (or an
/// over-deep tree) and was rejected as a whole. Callers treat this as a
/// structured validation failure: the content is omitted or the request fails
/// generically — raw CMS content is never emitted as a fallback (issue 072
/// AC-06; SECURITY.md, Logging rules 1–3).
/// </summary>
public sealed class DisallowedRichTextNodeException : InvalidOperationException
{
    public DisallowedRichTextNodeException(string message)
        : base(message)
    {
    }
}
