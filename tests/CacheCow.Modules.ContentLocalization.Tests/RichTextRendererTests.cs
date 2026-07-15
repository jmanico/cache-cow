using CacheCow.Modules.ContentLocalization.Rendering;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.ContentLocalization.Tests;

/// <summary>
/// Issue 072: CMS rich text renders only through the sanitizing allowlist
/// renderer — every text node encoded, only allowlisted nodes rendered,
/// unknown nodes reject the whole document (fail closed), hyperlink
/// destinations scheme-allowlisted with '#' fallback (CC-SEC-002, CC-CNT-001,
/// CC-SEC-004; SECURITY.md, Input validation rules 5 and 6).
/// </summary>
public sealed class RichTextRendererTests
{
    private static readonly AllowlistRichTextRenderer Renderer = new(new SchemeAllowlistUrlPolicy());

    private static RichTextTextNode Text(string text) => new(text);

    private static RichTextDocument Doc(params RichTextNode[] nodes) => new(nodes);

    [Fact]
    [Requirement("CC-SEC-002")]
    [Requirement("CC-CNT-001")]
    public void Each_allowlisted_node_type_renders_as_encoded_html()
    {
        var document = Doc(
            new RichTextHeadingNode(2, [Text("Meet our Chefs")]),
            new RichTextParagraphNode(
            [
                Text("Smoked "),
                new RichTextBoldNode([Text("low & slow")]),
                Text(" with "),
                new RichTextItalicNode([Text("real bark")]),
                Text("."),
            ]),
            new RichTextListNode(ordered: false,
            [
                new RichTextListItemNode([Text("Brisket")]),
                new RichTextListItemNode([new RichTextHyperlinkNode("https://example.com/cuts", [Text("All cuts")])]),
            ]),
            new RichTextListNode(ordered: true, [new RichTextListItemNode([Text("Step one")])]));

        var html = Renderer.Render(document);

        Assert.Equal(
            "<h2>Meet our Chefs</h2>" +
            "<p>Smoked <strong>low &amp; slow</strong> with <em>real bark</em>.</p>" +
            "<ul><li>Brisket</li><li><a href=\"https://example.com/cuts\">All cuts</a></li></ul>" +
            "<ol><li>Step one</li></ol>",
            html);
    }

    [Theory]
    [Requirement("CC-SEC-002")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("\"><iframe src=//evil>")]
    [InlineData("<svg/onload=alert(1)>")]
    [InlineData("<style>@import 'x';</style>")]
    [InlineData("]]><!--><script>alert(2)</script>")]
    public void Hostile_text_content_is_encoded_inert(string payload)
    {
        var html = Renderer.Render(Doc(new RichTextParagraphNode([Text(payload)])));

        // The only markup in the output is the renderer's own <p> wrapper; no
        // character of the payload survives as raw markup.
        Assert.StartsWith("<p>", html, StringComparison.Ordinal);
        Assert.EndsWith("</p>", html, StringComparison.Ordinal);
        var encodedPayload = html[3..^4];
        Assert.DoesNotContain("<", encodedPayload, StringComparison.Ordinal);
        Assert.DoesNotContain(">", encodedPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("\"", encodedPayload, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-SEC-002")]
    public void There_is_no_raw_html_node_in_the_model()
    {
        // The node model is the first allowlist layer: raw-HTML passthrough is
        // unrepresentable. Every concrete node type in the module either holds
        // plain text (always encoded) or typed children — none carries markup.
        var nodeTypes = typeof(RichTextNode).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(RichTextNode).IsAssignableFrom(t))
            .Select(t => t.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                nameof(RichTextBoldNode),
                nameof(RichTextHeadingNode),
                nameof(RichTextHyperlinkNode),
                nameof(RichTextItalicNode),
                nameof(RichTextListItemNode),
                nameof(RichTextListNode),
                nameof(RichTextParagraphNode),
                nameof(RichTextTextNode),
            ],
            nodeTypes);
        Assert.DoesNotContain(nodeTypes, name =>
            name.Contains("Raw", StringComparison.OrdinalIgnoreCase) || name.Contains("Html", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Requirement("CC-SEC-002")]
    public void A_node_outside_the_allowlist_rejects_the_whole_document()
    {
        // A "script node" or any unknown node type cannot be expressed in the
        // shipped model; even if one appears (future model drift, adapter
        // bugs), the renderer fails closed by rejecting the document — it is
        // never skipped or passed through (issue 072 failure behavior).
        var document = Doc(new UnknownNode());

        var exception = Assert.Throws<DisallowedRichTextNodeException>(() => Renderer.Render(document));

        Assert.Contains(nameof(UnknownNode), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-SEC-002")]
    public void A_disallowed_node_nested_deep_in_valid_content_still_rejects_everything()
    {
        var document = Doc(
            new RichTextParagraphNode([Text("fine")]),
            new RichTextListNode(ordered: false,
            [
                new RichTextListItemNode([new RichTextBoldNode([new UnknownNode()])]),
            ]));

        Assert.Throws<DisallowedRichTextNodeException>(() => Renderer.Render(document));
    }

    [Theory]
    [Requirement("CC-SEC-004")]
    [InlineData("javascript:alert(1)", "#")]
    [InlineData("JaVaScRiPt:alert(1)", "#")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=", "#")]
    [InlineData("vbscript:msgbox(1)", "#")]
    [InlineData("http://example.com/insecure", "#")]
    [InlineData("//example.com/protocol-relative", "#")]
    [InlineData("/relative/path", "#")]
    [InlineData("not a url", "#")]
    [InlineData("", "#")]
    [InlineData("https://example.com/ok", "https://example.com/ok")]
    [InlineData("mailto:howdy@cachecow.example", "mailto:howdy@cachecow.example")]
    [InlineData("tel:+1-555-0100", "tel:&#x2B;1-555-0100")] // '+' is attribute-entity-encoded; browsers decode it back
    public void Hyperlink_destinations_pass_the_scheme_allowlist_with_hash_fallback(string destination, string expectedHref)
    {
        var html = Renderer.Render(Doc(new RichTextHyperlinkNode(destination, [Text("link")])));

        Assert.Equal($"<a href=\"{expectedHref}\">link</a>", html);
    }

    [Fact]
    [Requirement("CC-SEC-004")]
    public void Hyperlink_href_output_is_attribute_encoded()
    {
        var html = Renderer.Render(Doc(
            new RichTextHyperlinkNode("https://example.com/?a=1&b=\"x\"", [Text("q")])));

        Assert.DoesNotContain("b=\"x\"", html, StringComparison.Ordinal);
        Assert.Contains("&amp;", html, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-SEC-002")]
    public void An_over_deep_tree_is_rejected_not_rendered()
    {
        RichTextNode node = Text("deep");
        for (var i = 0; i < 64; i++)
        {
            node = new RichTextBoldNode([node]);
        }

        Assert.Throws<DisallowedRichTextNodeException>(() => Renderer.Render(Doc(node)));
    }

    [Fact]
    [Requirement("CC-CNT-001")]
    public void Heading_levels_outside_1_to_6_are_unrepresentable()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RichTextHeadingNode(0, [Text("x")]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RichTextHeadingNode(7, [Text("x")]));
    }

    [Fact]
    [Requirement("CC-CNT-001")]
    public async Task The_content_source_port_returns_typed_trees_only()
    {
        var locale = CacheCow.SharedKernel.Locale.Parse("en-US");
        var document = Doc(new RichTextParagraphNode([Text("Chef bio")]));
        var source = new InMemoryContentSource(
            new Dictionary<(string, CacheCow.SharedKernel.Locale), RichTextDocument>
            {
                [("chefs.bio.1", locale)] = document,
            });

        Assert.Same(document, await source.FindDocumentAsync("chefs.bio.1", locale, TestContext.Current.CancellationToken));
        Assert.Null(await source.FindDocumentAsync("missing", locale, TestContext.Current.CancellationToken));
    }

    /// <summary>A node type the renderer does not know — must reject, never skip (fail closed).</summary>
    private sealed class UnknownNode : RichTextNode;
}
