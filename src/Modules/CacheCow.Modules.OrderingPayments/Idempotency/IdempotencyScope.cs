using CacheCow.Modules.OrderingPayments.Submission;

namespace CacheCow.Modules.OrderingPayments.Idempotency;

/// <summary>The server-side identity population an idempotency key belongs to (CC-SEC-015).</summary>
public enum IdempotencyScopeKind
{
    /// <summary>B2B partner tenant (Entra-issued client/tenant identity; SECURITY.md, Authentication rules 5–8).</summary>
    PartnerTenant = 0,

    /// <summary>Authenticated consumer account.</summary>
    ConsumerAccount = 1,

    /// <summary>Guest-checkout session (CC-ORD-001; how the session is issued is an open question, issue 037).</summary>
    GuestSession = 2,
}

/// <summary>
/// Composite idempotency scope: (identity population, server-derived
/// identifier). Keys are looked up by (scope, key), never by key alone, so one
/// partner's key can never collide with — or read — another's, and a guest
/// session's key is invisible to every other session (CC-SEC-015, CC-API-004).
/// The identifier comes exclusively from server-side authentication/session
/// state, never from a request field (SECURITY.md, Input validation rules 3
/// and 12).
/// </summary>
public readonly record struct IdempotencyScope
{
    private readonly string? _identifier;

    private IdempotencyScope(IdempotencyScopeKind kind, string identifier)
    {
        Kind = kind;
        _identifier = identifier;
    }

    public IdempotencyScopeKind Kind { get; }

    public string Identifier =>
        _identifier ?? throw new InvalidOperationException(
            "Uninitialized IdempotencyScope; use a For* factory (CC-SEC-015: an unscoped key is the exact threat-model finding).");

    public static IdempotencyScope ForPartnerTenant(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        return new IdempotencyScope(IdempotencyScopeKind.PartnerTenant, tenantId);
    }

    public static IdempotencyScope ForAccount(string accountId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        return new IdempotencyScope(IdempotencyScopeKind.ConsumerAccount, accountId);
    }

    public static IdempotencyScope ForGuestSession(string guestSessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(guestSessionId);
        return new IdempotencyScope(IdempotencyScopeKind.GuestSession, guestSessionId);
    }

    /// <summary>Scope for a consumer buyer: guest-session scope for guests, account scope for accounts (CC-SEC-015).</summary>
    public static IdempotencyScope ForBuyer(BuyerIdentity buyer)
    {
        ArgumentNullException.ThrowIfNull(buyer);
        return buyer.Kind switch
        {
            BuyerKind.GuestSession => ForGuestSession(buyer.Identifier),
            BuyerKind.Account => ForAccount(buyer.Identifier),
            _ => throw new ArgumentOutOfRangeException(nameof(buyer), buyer.Kind, "Unknown buyer kind."),
        };
    }

    public override string ToString() => $"{Kind}:{Identifier}";
}
