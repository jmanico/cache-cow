namespace CacheCow.Modules.ContentLocalization.Rendering;

/// <summary>
/// Typed node model for CMS (Contentful-style) rich text (CC-CNT-001). The
/// model is the first allowlist layer: there is deliberately NO node that
/// carries raw HTML — raw-HTML passthrough is unrepresentable, so no CMS
/// entry can smuggle markup past the renderer (CC-SEC-002; SECURITY.md, Input
/// validation rule 5). Text lives only in <see cref="RichTextTextNode"/>,
/// which the renderer always HTML-encodes. The renderer additionally rejects
/// any node type outside its allowlist, failing closed (defense in depth).
/// The concrete Contentful node-set mapping is an issue 072 open question;
/// this set covers document/paragraph/heading/bold/italic/list/list-item/
/// hyperlink/text.
/// </summary>
public abstract class RichTextNode
{
    protected RichTextNode()
    {
    }
}

/// <summary>The root of a rich-text tree.</summary>
public sealed class RichTextDocument
{
    public RichTextDocument(IReadOnlyList<RichTextNode> children)
    {
        Children = Copy(children);
    }

    public IReadOnlyList<RichTextNode> Children { get; }

    internal static IReadOnlyList<RichTextNode> Copy(IReadOnlyList<RichTextNode> children)
    {
        ArgumentNullException.ThrowIfNull(children);
        var copy = children.ToArray();
        if (copy.Any(c => c is null))
        {
            throw new ArgumentException("Rich-text children must not contain null nodes.", nameof(children));
        }

        return copy;
    }
}

/// <summary>Plain text. Untrusted; always HTML-encoded at render (CC-SEC-002).</summary>
public sealed class RichTextTextNode : RichTextNode
{
    public RichTextTextNode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
    }

    public string Text { get; }
}

/// <summary>A paragraph block.</summary>
public sealed class RichTextParagraphNode : RichTextNode
{
    public RichTextParagraphNode(IReadOnlyList<RichTextNode> children)
    {
        Children = RichTextDocument.Copy(children);
    }

    public IReadOnlyList<RichTextNode> Children { get; }
}

/// <summary>A heading block, level 1–6 (validated at construction; anything else is rejected).</summary>
public sealed class RichTextHeadingNode : RichTextNode
{
    public RichTextHeadingNode(int level, IReadOnlyList<RichTextNode> children)
    {
        if (level is < 1 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "Heading level must be 1-6.");
        }

        Level = level;
        Children = RichTextDocument.Copy(children);
    }

    public int Level { get; }

    public IReadOnlyList<RichTextNode> Children { get; }
}

/// <summary>Bold inline formatting.</summary>
public sealed class RichTextBoldNode : RichTextNode
{
    public RichTextBoldNode(IReadOnlyList<RichTextNode> children)
    {
        Children = RichTextDocument.Copy(children);
    }

    public IReadOnlyList<RichTextNode> Children { get; }
}

/// <summary>Italic inline formatting.</summary>
public sealed class RichTextItalicNode : RichTextNode
{
    public RichTextItalicNode(IReadOnlyList<RichTextNode> children)
    {
        Children = RichTextDocument.Copy(children);
    }

    public IReadOnlyList<RichTextNode> Children { get; }
}

/// <summary>An ordered or unordered list of list items.</summary>
public sealed class RichTextListNode : RichTextNode
{
    public RichTextListNode(bool ordered, IReadOnlyList<RichTextListItemNode> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var copy = items.ToArray();
        if (copy.Any(i => i is null))
        {
            throw new ArgumentException("List items must not contain null nodes.", nameof(items));
        }

        Ordered = ordered;
        Items = copy;
    }

    public bool Ordered { get; }

    public IReadOnlyList<RichTextListItemNode> Items { get; }
}

/// <summary>A single list item.</summary>
public sealed class RichTextListItemNode : RichTextNode
{
    public RichTextListItemNode(IReadOnlyList<RichTextNode> children)
    {
        Children = RichTextDocument.Copy(children);
    }

    public IReadOnlyList<RichTextNode> Children { get; }
}

/// <summary>
/// A hyperlink. The destination is untrusted data-derived href material and
/// is validated against the scheme allowlist (https/mailto/tel) with '#'
/// fallback at render time (CC-SEC-004; SECURITY.md, Input validation rule 6).
/// </summary>
public sealed class RichTextHyperlinkNode : RichTextNode
{
    public RichTextHyperlinkNode(string destination, IReadOnlyList<RichTextNode> children)
    {
        ArgumentNullException.ThrowIfNull(destination);
        Destination = destination;
        Children = RichTextDocument.Copy(children);
    }

    public string Destination { get; }

    public IReadOnlyList<RichTextNode> Children { get; }
}
