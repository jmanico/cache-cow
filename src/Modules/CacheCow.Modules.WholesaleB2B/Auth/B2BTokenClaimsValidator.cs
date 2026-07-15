using System.Collections.Frozen;
using System.Security.Claims;
using System.Text.Json;
using CacheCow.Modules.WholesaleB2B.Partners;

namespace CacheCow.Modules.WholesaleB2B.Auth;

/// <summary>
/// Port through which every B2B request turns an already-authenticated
/// principal into a <see cref="B2BClientContext"/> (CC-API-002/003).
/// </summary>
public interface IB2BTokenClaimsValidator
{
    B2BTokenValidationResult Validate(ClaimsPrincipal principal);
}

/// <summary>
/// Outcome of claim-policy validation. <see cref="Context"/> is non-null
/// exactly when <see cref="Succeeded"/> is true. <see cref="FailureReason"/>
/// is a fixed, generic vocabulary safe for structured security logs — it never
/// contains claim or token values (SECURITY.md, Logging rules 1 and 4); HTTP
/// responses stay a generic 401 regardless (issue 054, Failure Behavior).
/// </summary>
public sealed class B2BTokenValidationResult
{
    private B2BTokenValidationResult(B2BClientContext? context, string? failureReason)
    {
        Context = context;
        FailureReason = failureReason;
    }

    public bool Succeeded => Context is not null;

    public B2BClientContext? Context { get; }

    public string? FailureReason { get; }

    internal static B2BTokenValidationResult Success(B2BClientContext context) => new(context, null);

    internal static B2BTokenValidationResult Failure(string reason) => new(null, reason);
}

/// <summary>
/// The claim-level token policy this module owns (issue 054). JWT signature,
/// issuer, audience, lifetime-expiry, and algorithm validation are the HOST's
/// JwtBearer configuration per SECURITY.md Authentication rule 7 (all four
/// validations on, pinned <c>ValidAlgorithms</c>, audience = this API's
/// resource identifier, clock skew ≤ 2 minutes) — see the host contract on
/// <see cref="WholesaleB2BModule"/>. This validator enforces what remains
/// enforceable at the resource server, on claims a verified signature vouches
/// for:
/// - a client identifier must be present and registered (port:
///   <see cref="IB2BClientDirectory"/>) for an Approved partner tenant,
/// - issued token lifetime (<c>exp - iat</c>) ≤ 15 minutes (CC-API-003) with
///   ≤ 2 minutes of clock-skew allowance on the expiry re-check (ratified
///   2026-07-15) — a token minted with a longer life is rejected even though
///   it is not yet expired,
/// - scopes must all belong to the closed CC-API-004 set (unknown scope =
///   rejection, fail closed),
/// - sender-constraining is recorded from <c>cnf</c> (RFC 8705 / RFC 9449);
///   bearer-only tokens are ceilinged to read-only effective scopes
///   (CC-API-003).
/// Every failure — including an exception anywhere in the path — is a denial
/// (SECURITY.md, Logging rule 2).
/// </summary>
public sealed class B2BTokenClaimsValidator : IB2BTokenClaimsValidator
{
    /// <summary>CC-API-003: access tokens are short-lived, max 15 minutes.</summary>
    public static readonly TimeSpan MaxTokenLifetime = TimeSpan.FromMinutes(15);

    /// <summary>Ratified 2026-07-15 (ARCHITECTURE.md, Decision record): clock skew ≤ 2 minutes.</summary>
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(2);

    private readonly IB2BClientDirectory _directory;
    private readonly TimeProvider _timeProvider;

    public B2BTokenClaimsValidator(IB2BClientDirectory directory, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(directory);
        _directory = directory;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public B2BTokenValidationResult Validate(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.Identity is not { IsAuthenticated: true })
        {
            return B2BTokenValidationResult.Failure("unauthenticated");
        }

        var clientId = B2BTokenClaims.ClientId(principal);
        if (clientId is null)
        {
            return B2BTokenValidationResult.Failure("missing-client-id");
        }

        // Lifetime policy (CC-API-003). iat and exp must both be present and
        // coherent: a token that cannot prove it was minted short-lived is
        // rejected (fail closed), not assumed compliant.
        var issuedAt = B2BTokenClaims.UnixSeconds(principal, "iat");
        var expiresAt = B2BTokenClaims.UnixSeconds(principal, "exp");
        if (issuedAt is null || expiresAt is null || expiresAt <= issuedAt)
        {
            return B2BTokenValidationResult.Failure("unverifiable-lifetime");
        }

        if (expiresAt - issuedAt > (long)MaxTokenLifetime.TotalSeconds)
        {
            return B2BTokenValidationResult.Failure("lifetime-exceeds-maximum");
        }

        // Defense in depth on top of the host's ValidateLifetime: re-check
        // expiry here with the ratified ≤ 2 minute skew.
        var nowSeconds = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var skewSeconds = (long)MaxClockSkew.TotalSeconds;
        if (expiresAt + skewSeconds < nowSeconds || issuedAt - skewSeconds > nowSeconds)
        {
            return B2BTokenValidationResult.Failure("outside-validity-window");
        }

        // Closed scope model (CC-API-004): any scope outside the set rejects
        // the whole token — never filtered into acceptance (SECURITY.md,
        // Input validation rule 1).
        var scopeValues = B2BTokenClaims.ScopeValues(principal);
        foreach (var scope in scopeValues)
        {
            if (!B2BScopes.All.Contains(scope))
            {
                return B2BTokenValidationResult.Failure("unknown-scope");
            }
        }

        var granted = scopeValues.ToFrozenSet(StringComparer.Ordinal);

        var senderConstraint = ParseSenderConstraint(principal);
        var effective = senderConstraint == B2BSenderConstraint.None
            ? granted.Where(B2BScopes.ReadOnly.Contains).ToFrozenSet(StringComparer.Ordinal)
            : granted;

        // Client-id -> partner tenancy (port). Unknown client or any directory
        // failure is a denial; approval is re-checked per request so a
        // suspension is effective immediately (issue 049 semantics reused).
        PartnerTenantContext tenant;
        try
        {
            var partnerTenant = _directory.FindByClientId(clientId);
            if (partnerTenant is null)
            {
                return B2BTokenValidationResult.Failure("unregistered-client");
            }

            tenant = PartnerTenantContext.ForApprovedTenant(partnerTenant);
        }
        catch (PartnerNotApprovedException)
        {
            return B2BTokenValidationResult.Failure("partner-not-approved");
        }
        catch (Exception)
        {
            // Fail closed: a directory fault is a denial, never a bypass
            // (SECURITY.md, Logging rule 2).
            return B2BTokenValidationResult.Failure("client-directory-unavailable");
        }

        return B2BTokenValidationResult.Success(
            new B2BClientContext(clientId, tenant, senderConstraint, granted, effective));
    }

    /// <summary>
    /// Reads the <c>cnf</c> confirmation claim (RFC 7800): <c>x5t#S256</c> is
    /// an mTLS-bound token (RFC 8705), <c>jkt</c> a DPoP-bound one (RFC 9449).
    /// Anything absent, malformed, or unrecognized degrades to bearer-only —
    /// the weakest privilege, read-only ceiling (fail closed).
    /// </summary>
    private static B2BSenderConstraint ParseSenderConstraint(ClaimsPrincipal principal)
    {
        var confirmation = B2BTokenClaims.ConfirmationClaim(principal);
        if (string.IsNullOrWhiteSpace(confirmation))
        {
            return B2BSenderConstraint.None;
        }

        try
        {
            using var parsed = JsonDocument.Parse(confirmation);
            if (parsed.RootElement.ValueKind != JsonValueKind.Object)
            {
                return B2BSenderConstraint.None;
            }

            if (parsed.RootElement.TryGetProperty("x5t#S256", out var thumbprint)
                && thumbprint.ValueKind == JsonValueKind.String)
            {
                return B2BSenderConstraint.MutualTls;
            }

            if (parsed.RootElement.TryGetProperty("jkt", out var keyThumbprint)
                && keyThumbprint.ValueKind == JsonValueKind.String)
            {
                return B2BSenderConstraint.DPoP;
            }

            return B2BSenderConstraint.None;
        }
        catch (JsonException)
        {
            return B2BSenderConstraint.None;
        }
    }
}
