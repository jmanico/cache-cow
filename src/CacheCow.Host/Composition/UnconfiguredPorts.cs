using System.Net;
using CacheCow.Modules.OrderingPayments.Payments;
using CacheCow.Modules.OrderingPayments.Submission;
using CacheCow.Modules.OrderingPayments.Webhooks;
using CacheCow.Modules.WholesaleB2B.Auth;
using CacheCow.Modules.WholesaleB2B.Partners;
using CacheCow.Modules.WholesaleB2B.Webhooks;
using CacheCow.SharedKernel;

namespace CacheCow.Host.Composition;

/// <summary>
/// Fail-closed host defaults for cross-context ports whose REAL adapters are
/// later issues (Entra client registrations, Stripe/Razorpay clients and Tax,
/// Key Vault secret sources, the outbound webhook HTTP transport). Every
/// member throws — never a silent no-op, never a permissive stub — so any
/// path that reaches an unwired external system is denied and the
/// misconfiguration is visible (SECURITY.md, Logging rule 2). Consumers of
/// these ports all treat a throw as a denial by contract. Each type names the
/// issue that replaces it.
/// </summary>
internal static class UnconfiguredPort
{
    internal static InvalidOperationException Failure(string portName, string replacedBy) =>
        new($"{portName} has no configured adapter yet (wired by {replacedBy}); failing closed — this operation is denied until the real adapter lands (SECURITY.md, Logging rule 2).");
}

/// <summary>Entra ID client-registration directory (CC-API-002) — real adapter is issues 054/058.</summary>
internal sealed class UnconfiguredB2BClientDirectory : IB2BClientDirectory
{
    /// <inheritdoc />
    public PartnerTenant? FindByClientId(string clientId) =>
        throw UnconfiguredPort.Failure(nameof(IB2BClientDirectory), "the Entra ID integration (issues 054/058)");
}

/// <summary>External tax computation (Stripe Tax / Razorpay, CC-PRC-002) — real adapters are issues 039/040. A submission never proceeds with guessed tax.</summary>
internal sealed class UnconfiguredTaxCalculator : ITaxCalculator
{
    /// <inheritdoc />
    public Money CalculateTax(Market market, Money taxableTotal) =>
        throw UnconfiguredPort.Failure(nameof(ITaxCalculator), "the Stripe Tax / Razorpay adapters (issues 039/040)");
}

/// <summary>Server-initiated payment status check (CC-ORD-009) — real adapters are issues 039/040. Nothing advances on an unverifiable payment.</summary>
internal sealed class UnconfiguredProcessorStatusClient : IProcessorStatusClient
{
    /// <inheritdoc />
    public ProcessorPaymentStatus GetPaymentStatus(string processorName, string paymentReference) =>
        throw UnconfiguredPort.Failure(nameof(IProcessorStatusClient), "the Stripe/Razorpay adapters (issues 039/040)");
}

/// <summary>Key Vault-backed inbound processor-webhook verification secrets (CC-SEC-014; SECURITY.md, Secret handling rule 9) — a later Key Vault issue.</summary>
internal sealed class UnconfiguredSigningSecretProvider : ISigningSecretProvider
{
    /// <inheritdoc />
    public IReadOnlyList<byte[]> GetSigningSecrets(string processorName) =>
        throw UnconfiguredPort.Failure(nameof(ISigningSecretProvider), "the Key Vault secret adapter (later issue)");
}

/// <summary>Real DNS resolution for the outbound-webhook SSRF policy (CC-API-009; SECURITY.md, Input validation rule 8) — no registration or delivery proceeds unvalidated.</summary>
internal sealed class UnconfiguredWebhookAddressResolver : IWebhookAddressResolver
{
    /// <inheritdoc />
    public IReadOnlyList<IPAddress> Resolve(string hostName) =>
        throw UnconfiguredPort.Failure(nameof(IWebhookAddressResolver), "the outbound webhook delivery infrastructure (later issue)");
}

/// <summary>Key Vault-backed per-partner rotating webhook HMAC secrets (SECURITY.md, Secret handling rule 8) — a later Key Vault issue.</summary>
internal sealed class UnconfiguredWebhookSecretSource : IWebhookSecretSource
{
    /// <inheritdoc />
    public WebhookSigningSecret CurrentSecretFor(PartnerId partnerId) =>
        throw UnconfiguredPort.Failure(nameof(IWebhookSecretSource), "the Key Vault secret adapter (later issue)");
}

/// <summary>No-redirect, address-pinning HTTPS webhook transport (CC-API-009) — a later delivery-infrastructure issue.</summary>
internal sealed class UnconfiguredWebhookDeliveryTransport : IWebhookDeliveryTransport
{
    /// <inheritdoc />
    public Task DeliverAsync(SignedWebhookDelivery delivery, CancellationToken cancellationToken) =>
        throw UnconfiguredPort.Failure(nameof(IWebhookDeliveryTransport), "the outbound webhook delivery infrastructure (later issue)");
}

/// <summary>
/// Wholesale-invoice read port (CC-WHS-004, CC-API-004). The Invoicing
/// context has no issued-invoice query store yet, so there is no real target
/// to adapt to — a flagged gap, not resolved here. Reads throw (surfacing as
/// a generic 500 problem, fail closed) rather than fabricating "not found".
/// </summary>
internal sealed class UnconfiguredWholesaleInvoiceReader : CacheCow.Modules.WholesaleB2B.Invoices.IWholesaleInvoiceReader
{
    /// <inheritdoc />
    public CacheCow.Modules.WholesaleB2B.Invoices.WholesaleInvoiceSummary? FindInvoice(
        PartnerTenantContext context, string invoiceId) =>
        throw UnconfiguredPort.Failure(
            nameof(CacheCow.Modules.WholesaleB2B.Invoices.IWholesaleInvoiceReader),
            "the Invoicing issued-invoice store adapter (later issue)");
}
