namespace CacheCow.Modules.WholesaleB2B.Auth;

/// <summary>
/// How an access token is sender-constrained (CC-API-003; SECURITY.md,
/// Authentication rule 6), recorded from the token's <c>cnf</c> claim.
/// Cryptographic proof-of-possession verification (the mTLS client certificate
/// thumbprint match per RFC 8705, or the DPoP proof per RFC 9449) is transport
/// machinery owned by the host's authentication pipeline; this module records
/// the constraint and enforces the claim-level policy: a token with no
/// recognized constraint is bearer-only and is ceilinged to read-only scopes.
/// How deep the resource server itself must verify DPoP/mTLS binding is an
/// open question (issue 054, Open Questions) — not resolved here.
/// </summary>
public enum B2BSenderConstraint
{
    /// <summary>No recognized <c>cnf</c> confirmation: bearer-only, read-only ceiling (CC-API-003).</summary>
    None = 0,

    /// <summary>Certificate-bound per RFC 8705 (<c>cnf.x5t#S256</c>).</summary>
    MutualTls = 1,

    /// <summary>DPoP key-bound per RFC 9449 (<c>cnf.jkt</c>).</summary>
    DPoP = 2,
}
