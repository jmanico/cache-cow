namespace CacheCow.Modules.OrderingPayments.Submission;

/// <summary>The two buyer populations at consumer checkout (CC-ORD-001): guest sessions and optional accounts.</summary>
public enum BuyerKind
{
    /// <summary>Guest checkout — no account, identified only by the server-issued guest-checkout session (CC-ORD-001).</summary>
    GuestSession = 0,

    /// <summary>Authenticated consumer account (optional at checkout, CC-ORD-001).</summary>
    Account = 1,
}

/// <summary>
/// Server-derived buyer identity attached to a submission. Guest checkout is
/// first-class in every market and account creation is optional (CC-ORD-001).
/// This value comes exclusively from server-side session/authentication state
/// — it is a service parameter, never a field of the client-bound submission
/// DTO (SECURITY.md, Input validation rule 3).
///
/// How a guest-checkout session is issued/identified is an open question
/// (issue 037, Open Questions; depends on issue 036's cart-model question):
/// this type only requires that the server hands it an opaque identifier.
/// </summary>
public sealed record BuyerIdentity
{
    private BuyerIdentity(BuyerKind kind, string identifier)
    {
        Kind = kind;
        Identifier = identifier;
    }

    public BuyerKind Kind { get; }

    /// <summary>Opaque server-side identifier: guest-checkout session id or account id.</summary>
    public string Identifier { get; }

    public static BuyerIdentity ForGuestSession(string guestSessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(guestSessionId);
        return new BuyerIdentity(BuyerKind.GuestSession, guestSessionId);
    }

    public static BuyerIdentity ForAccount(string accountId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        return new BuyerIdentity(BuyerKind.Account, accountId);
    }
}
