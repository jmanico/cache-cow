namespace CacheCow.Modules.ContentLocalization.Email;

/// <summary>
/// Port for handing a composed transactional email to the delivery provider.
/// The Azure Communication Services adapter is a later issue (managed
/// identity, SECURITY.md Secret handling rule 2; sender-domain SPF/DKIM/DMARC
/// is issue 093, CC-SEC-018). Adapters MUST NOT copy body content, tracking
/// links, or any capability token into provider metadata or logged headers
/// (SECURITY.md, Email and messaging security rule 1; Logging rule 4).
/// </summary>
public interface IEmailDispatch
{
    Task DispatchAsync(ComposedOrderEmail email, string recipientEmailAddress, CancellationToken cancellationToken);
}

/// <summary>A dispatched message as recorded by the in-memory port.</summary>
public sealed record DispatchedEmail(ComposedOrderEmail Email, string RecipientEmailAddress);

/// <summary>In-memory recording dispatcher for tests and the provisional module default.</summary>
public sealed class InMemoryEmailDispatch : IEmailDispatch
{
    private readonly List<DispatchedEmail> _dispatched = [];

    public IReadOnlyList<DispatchedEmail> Dispatched => _dispatched;

    public Task DispatchAsync(ComposedOrderEmail email, string recipientEmailAddress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientEmailAddress);
        _dispatched.Add(new DispatchedEmail(email, recipientEmailAddress));
        return Task.CompletedTask;
    }
}
