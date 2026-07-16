using CacheCow.Modules.Invoicing.Access;
using CacheCow.Modules.Invoicing.Invoices;
using CacheCow.Modules.OrderingPayments.GuestAccess;

namespace CacheCow.Host.Composition;

/// <summary>
/// Invoicing <see cref="IGuestOrderCapabilityTokenValidator"/> → OrderingPayments
/// <see cref="GuestAccessTokenService"/> (CC-ORD-010, CC-SEC-017, CC-INV-002;
/// SECURITY.md, Authentication rule 14). The token type and its lifecycle live
/// in Ordering &amp; Payments; Invoicing only consumes the verdict through this
/// host adapter — token logic is never duplicated (ARCHITECTURE.md, Dependency
/// rule 9).
///
/// Invoice access validates tokens bound to the
/// <see cref="GuestAccessPurpose.InvoiceDownload"/> purpose only: a token
/// minted for order status or tracking grants nothing here (single-purpose
/// binding). Expired, revoked, unknown, malformed, wrong-purpose, and faulted
/// validations are all the same indistinguishable
/// <see cref="GuestCapabilityTokenValidation.Invalid"/> — the service already
/// compares digests in constant time and never logs the presented value.
/// </summary>
internal sealed class GuestOrderCapabilityTokenValidatorAdapter : IGuestOrderCapabilityTokenValidator
{
    private readonly GuestAccessTokenService _tokens;

    public GuestOrderCapabilityTokenValidatorAdapter(GuestAccessTokenService tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        _tokens = tokens;
    }

    /// <inheritdoc />
    public GuestCapabilityTokenValidation Validate(string presentedToken)
    {
        // TryAuthorize is fail-closed by contract: every failure — including
        // an internal exception — is the same boolean false.
        if (!_tokens.TryAuthorize(presentedToken, GuestAccessPurpose.InvoiceDownload, out var orderId))
        {
            return GuestCapabilityTokenValidation.Invalid();
        }

        return OrderReference.TryParse(orderId.ToString(), out var boundOrder)
            ? GuestCapabilityTokenValidation.ValidFor(boundOrder)
            : GuestCapabilityTokenValidation.Invalid();
    }
}
