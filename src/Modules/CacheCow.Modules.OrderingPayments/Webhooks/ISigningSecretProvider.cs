namespace CacheCow.Modules.OrderingPayments.Webhooks;

/// <summary>
/// Port resolving the named signing secrets used to verify inbound processor
/// webhooks (issue 041; CC-SEC-014). The production adapter sources secrets
/// from Azure Key Vault, scoped per environment, with rotation on the
/// processor's schedule (SECURITY.md, Secret handling rule 9) — that adapter
/// is a later issue; this module only consumes the port.
///
/// Contract:
/// - Returns every currently acceptable secret for the processor — the active
///   secret first, plus the previous one during a rotation window, so rotation
///   never causes verification downtime (issue 041, AC-06).
/// - Returns an empty list when the processor has no configured secret; the
///   verifier then rejects (fails closed), it never skips verification.
/// - Throws on retrieval failure (e.g. Key Vault unavailable); the verifier
///   treats a throw as a rejection, never as acceptance (SECURITY.md, Logging
///   rule 2).
/// - Secret material is never logged and never appears in exception messages
///   (SECURITY.md, Secret handling rule 1; Logging rule 4).
/// </summary>
public interface ISigningSecretProvider
{
    /// <summary>All currently acceptable HMAC signing secrets for the named processor.</summary>
    IReadOnlyList<byte[]> GetSigningSecrets(string processorName);
}
