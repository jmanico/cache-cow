using System.Security.Cryptography;

namespace CacheCow.Modules.OrderingPayments.Webhooks;

/// <summary>
/// The single gate between an untrusted inbound processor callback and any
/// downstream processing (issue 041; CC-SEC-014; SECURITY.md, Input
/// validation rule 11). Verifies an HMAC-SHA256 signature over the RAW
/// request body bytes — before any parsing — with constant-time comparison,
/// then enforces the timestamp replay bound and the seen-event-id (nonce)
/// check. Only on full success does a <see cref="VerifiedProcessorEvent"/>
/// exist; there is no other way to construct one.
///
/// Processor-agnostic by design: Stripe/Razorpay wire formats (signature
/// headers, signed-content composition) are the adapters' concern
/// (issues 039/040), and the same component serves the other untrusted
/// callback senders named by SECURITY.md Input validation rule 11
/// (Contentful, EasyPost) through the same port surface.
///
/// Fail-closed throughout (issue 041, AC-08): a missing/invalid/malformed
/// signature, an unresolvable secret, a stale timestamp, a replayed id, or
/// ANY exception inside verification yields a typed
/// <see cref="WebhookRejection"/> the host logs and alerts as a security
/// event — never acceptance. Secrets pass through here without ever being
/// logged or echoed (SECURITY.md, Secret handling rule 9; Logging rule 4).
/// </summary>
public sealed class WebhookVerifier
{
    private const int HmacSha256Length = 32;

    private readonly ISigningSecretProvider _secretProvider;
    private readonly IWebhookReplayStore _replayStore;
    private readonly WebhookVerificationOptions _options;
    private readonly TimeProvider _timeProvider;

    public WebhookVerifier(
        ISigningSecretProvider secretProvider,
        IWebhookReplayStore replayStore,
        WebhookVerificationOptions options,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(secretProvider);
        ArgumentNullException.ThrowIfNull(replayStore);
        ArgumentNullException.ThrowIfNull(options);

        _secretProvider = secretProvider;
        _replayStore = replayStore;
        _options = options;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Verifies one delivery. Check order is deliberate: signature over the
    /// raw body first (nothing about an unauthenticated payload is trusted),
    /// then the timestamp bound, then nonce registration — so a forged
    /// delivery can never consume an event id and block the authentic one.
    /// </summary>
    public WebhookVerificationResult Verify(UnverifiedWebhookDelivery delivery)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        try
        {
            if (string.IsNullOrWhiteSpace(delivery.SignatureHex))
            {
                return Reject(delivery, WebhookRejectionReason.MissingSignature);
            }

            byte[] presentedSignature;
            try
            {
                presentedSignature = Convert.FromHexString(delivery.SignatureHex);
            }
            catch (FormatException)
            {
                return Reject(delivery, WebhookRejectionReason.MalformedSignature);
            }

            if (presentedSignature.Length != HmacSha256Length)
            {
                return Reject(delivery, WebhookRejectionReason.MalformedSignature);
            }

            IReadOnlyList<byte[]> secrets;
            try
            {
                secrets = _secretProvider.GetSigningSecrets(delivery.ProcessorName);
            }
            catch (Exception)
            {
                // Key Vault (or adapter) failure: deny, never skip verification
                // (SECURITY.md, Logging rule 2; issue 041, Failure Behavior).
                return Reject(delivery, WebhookRejectionReason.SigningSecretUnavailable);
            }

            if (secrets is null || secrets.Count == 0)
            {
                return Reject(delivery, WebhookRejectionReason.SigningSecretUnavailable);
            }

            var signatureValid = false;
            foreach (var secret in secrets)
            {
                var computed = HMACSHA256.HashData(secret, delivery.RawBody.Span);
                if (CryptographicOperations.FixedTimeEquals(computed, presentedSignature))
                {
                    // Any currently acceptable secret matches — supports
                    // rotation without downtime (issue 041, AC-06).
                    signatureValid = true;
                }
            }

            if (!signatureValid)
            {
                return Reject(delivery, WebhookRejectionReason.InvalidSignature);
            }

            if (delivery.Timestamp is not { } timestamp)
            {
                return Reject(delivery, WebhookRejectionReason.MissingTimestamp);
            }

            var now = _timeProvider.GetUtcNow();
            var age = now - timestamp;
            if (age > _options.MaxEventAge || age < -_options.MaxEventAge)
            {
                return Reject(delivery, WebhookRejectionReason.StaleTimestamp);
            }

            if (string.IsNullOrWhiteSpace(delivery.EventId))
            {
                return Reject(delivery, WebhookRejectionReason.MissingEventId);
            }

            if (!_replayStore.TryRegister(delivery.ProcessorName, delivery.EventId, now))
            {
                return Reject(delivery, WebhookRejectionReason.ReplayedEventId);
            }

            return WebhookVerificationResult.Verified(new VerifiedProcessorEvent(
                delivery.ProcessorName,
                delivery.EventId,
                timestamp,
                delivery.RawBody,
                now));
        }
        catch (Exception)
        {
            // Any exception in the verification path is a denial, never a
            // bypass (issue 041, AC-08; SECURITY.md, Logging rule 2).
            return Reject(delivery, WebhookRejectionReason.VerificationError);
        }
    }

    private WebhookRejection RejectionFor(UnverifiedWebhookDelivery delivery, WebhookRejectionReason reason) =>
        new(delivery.ProcessorName, reason, _timeProvider.GetUtcNow());

    private WebhookVerificationResult Reject(UnverifiedWebhookDelivery delivery, WebhookRejectionReason reason) =>
        WebhookVerificationResult.Rejected(RejectionFor(delivery, reason));
}
