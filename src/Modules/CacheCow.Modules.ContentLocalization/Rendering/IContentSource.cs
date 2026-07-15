using CacheCow.SharedKernel;

namespace CacheCow.Modules.ContentLocalization.Rendering;

/// <summary>
/// Port for retrieving CMS content as a typed rich-text tree. The Contentful
/// delivery adapter (and the signature-verified publish-event receiver,
/// SECURITY.md Input validation rule 11) are later issues; whatever the
/// adapter, content arrives here already mapped to the typed node model and
/// still renders exclusively through <see cref="AllowlistRichTextRenderer"/>
/// (CC-CNT-001, CC-SEC-002). Any caching in front of this port must key on
/// transacting market + locale (CC-MKT-009; SECURITY.md, HTTP boundary
/// rule 10).
/// </summary>
public interface IContentSource
{
    /// <summary>Returns the document for a content id and locale, or null when it does not exist.</summary>
    Task<RichTextDocument?> FindDocumentAsync(string contentId, Locale locale, CancellationToken cancellationToken);
}

/// <summary>In-memory source for tests and the provisional module default.</summary>
public sealed class InMemoryContentSource : IContentSource
{
    private readonly IReadOnlyDictionary<(string ContentId, Locale Locale), RichTextDocument> _documents;

    public InMemoryContentSource()
        : this(new Dictionary<(string, Locale), RichTextDocument>())
    {
    }

    public InMemoryContentSource(IReadOnlyDictionary<(string ContentId, Locale Locale), RichTextDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        _documents = documents;
    }

    public Task<RichTextDocument?> FindDocumentAsync(string contentId, Locale locale, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentId);
        return Task.FromResult(_documents.TryGetValue((contentId, locale), out var document) ? document : null);
    }
}
