using CacheCow.Modules.Invoicing.Invoices;

namespace CacheCow.Modules.Invoicing.Access;

/// <summary>
/// Port: validation of a presented CC-ORD-010 guest-order capability token.
/// The token TYPE and its lifecycle (minting, ≥ 128 bits entropy, expiry,
/// server-side revocation, single-order binding) live in the Ordering &amp;
/// Payments context (issue 042; SECURITY.md, Authentication rule 14) — the
/// host adapts that implementation to this interface. This module never
/// duplicates token logic; it only consumes the validation verdict
/// (ARCHITECTURE.md, Dependency rule 9: cross-context needs are ports).
///
/// Contract: expired, revoked, unknown, and malformed tokens are all simply
/// invalid — indistinguishable to the caller; implementations use
/// constant-time comparison and never log the presented value (SECURITY.md,
/// Authentication rule 14; Logging rule 4).
/// </summary>
public interface IGuestOrderCapabilityTokenValidator
{
    GuestCapabilityTokenValidation Validate(string presentedToken);
}

/// <summary>
/// Verdict of a capability-token validation: either invalid (no detail — the
/// reasons are deliberately indistinguishable) or valid and bound to exactly
/// one order (CC-ORD-010).
/// </summary>
public sealed class GuestCapabilityTokenValidation
{
    private static readonly GuestCapabilityTokenValidation InvalidInstance = new(null);

    private GuestCapabilityTokenValidation(OrderReference? boundOrder)
    {
        BoundOrder = boundOrder;
    }

    public bool IsValid => BoundOrder is not null;

    /// <summary>The single order the token is bound to; null when invalid.</summary>
    public OrderReference? BoundOrder { get; }

    public static GuestCapabilityTokenValidation Invalid() => InvalidInstance;

    public static GuestCapabilityTokenValidation ValidFor(OrderReference boundOrder)
    {
        _ = boundOrder.Value; // an uninitialized binding fails closed
        return new GuestCapabilityTokenValidation(boundOrder);
    }
}
