using System.Collections.Frozen;
using CacheCow.Modules.WholesaleB2B.Partners;

namespace CacheCow.Modules.WholesaleB2B.Auth;

/// <summary>
/// The authenticated B2B API caller (CC-API-002/003/004): the OAuth2 client
/// identity, its partner tenancy (reusing <see cref="PartnerTenantContext"/> —
/// the same tenant-scoping key the wholesale portal uses, CC-WHS-003), the
/// scopes the token granted, and the sender-constraining posture. Minted only
/// by <see cref="B2BTokenClaimsValidator"/> from an already
/// signature-validated principal; endpoints resolve every piece of data
/// strictly through <see cref="Tenant"/>, so cross-tenant reads are
/// unrepresentable (SECURITY.md, Authentication rules 8–9).
/// </summary>
public sealed class B2BClientContext
{
    internal B2BClientContext(
        string clientId,
        PartnerTenantContext tenant,
        B2BSenderConstraint senderConstraint,
        FrozenSet<string> grantedScopes,
        FrozenSet<string> effectiveScopes)
    {
        ClientId = clientId;
        Tenant = tenant;
        SenderConstraint = senderConstraint;
        GrantedScopes = grantedScopes;
        EffectiveScopes = effectiveScopes;
    }

    /// <summary>The OAuth2 client id — the rate-limit partition key (CC-API-008) and idempotency scope (CC-SEC-015).</summary>
    public string ClientId { get; }

    /// <summary>The partner tenancy every query is scoped to (CC-API-004; SECURITY.md, Authentication rule 9).</summary>
    public PartnerTenantContext Tenant { get; }

    /// <summary>Recorded sender-constraining posture (CC-API-003; issue 054).</summary>
    public B2BSenderConstraint SenderConstraint { get; }

    /// <summary>The scopes the token carried, all drawn from <see cref="B2BScopes.All"/>.</summary>
    public IReadOnlySet<string> GrantedScopes { get; }

    /// <summary>
    /// The scopes this request may actually exercise: identical to
    /// <see cref="GrantedScopes"/> for sender-constrained tokens; the
    /// read-only intersection for bearer-only tokens (CC-API-003 — a
    /// bearer-only token presenting <c>orders:write</c> is denied on every
    /// mutating endpoint).
    /// </summary>
    public IReadOnlySet<string> EffectiveScopes { get; }

    public bool HasScope(string scope) => EffectiveScopes.Contains(scope);
}
