using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CacheCow.Modules.WholesaleB2B.Partners;

namespace CacheCow.Modules.WholesaleB2B.Webhooks;

/// <summary>
/// One partner's current webhook signing secret. Secrets are per partner and
/// rotating (SECURITY.md, Secret handling rule 8): <see cref="KeyId"/> names
/// the rotation generation so receivers can select verification material
/// during a rotation window. Material must be at least 256 bits. The value is
/// itself a secret: never logged, never serialized into responses
/// (SECURITY.md, Logging rule 4).
/// </summary>
public sealed class WebhookSigningSecret
{
    public const int MinimumMaterialLength = 32;

    public WebhookSigningSecret(string keyId, ReadOnlyMemory<byte> material)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        if (material.Length < MinimumMaterialLength)
        {
            throw new WholesaleValidationException(
                $"Webhook signing secrets require at least {MinimumMaterialLength} bytes of material (SECURITY.md, Secret handling rule 8).");
        }

        KeyId = keyId;
        Material = material;
    }

    public string KeyId { get; }

    public ReadOnlyMemory<byte> Material { get; }
}

/// <summary>
/// Port supplying the CURRENT per-partner rotating HMAC secret (CC-API-009;
/// SECURITY.md, Secret handling rules 1, 5, 8). The host adapts Azure Key
/// Vault via managed identity; secrets are cached only with a TTL honoring
/// expiry and react to rotation events. Contract: throw when no secret can be
/// retrieved — delivery for that partner halts rather than falling back to
/// any cached-forever or shared default secret (issue 057, Failure Behavior).
/// One partner's secret is never used for another's deliveries.
/// </summary>
public interface IWebhookSecretSource
{
    WebhookSigningSecret CurrentSecretFor(PartnerId partnerId);
}

/// <summary>
/// The webhook signing envelope (CC-API-009; SECURITY.md, Secret handling
/// rule 8): HMAC-SHA256 over <c>"{unix-timestamp}.{body}"</c>, transported as
/// <c>CacheCow-Webhook-Signature: v1=&lt;lowercase hex&gt;</c> with the
/// timestamp and rotation key id in companion headers. Binding the timestamp
/// into the signed payload bounds replay: a captured delivery cannot be
/// replayed outside the receiver's acceptance window, and the timestamp
/// cannot be altered without invalidating the signature.
/// The exact transport format and replay window are not ratified in the specs
/// (issue 057, Open Questions); these are this module's documented defaults,
/// published to partners via the generated API document.
/// </summary>
public static class WebhookSigner
{
    public const string SignatureHeader = "CacheCow-Webhook-Signature";
    public const string TimestampHeader = "CacheCow-Webhook-Timestamp";
    public const string KeyIdHeader = "CacheCow-Webhook-Key-Id";
    public const string SignatureScheme = "v1";

    /// <summary>Default receiver-side replay acceptance window (module default; open question in issue 057).</summary>
    public static readonly TimeSpan DefaultReplayTolerance = TimeSpan.FromMinutes(5);

    public static string Sign(WebhookSigningSecret secret, long unixTimestampSeconds, string body)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(body);

        var signedPayload = Encoding.UTF8.GetBytes(
            unixTimestampSeconds.ToString(CultureInfo.InvariantCulture) + "." + body);
        var mac = HMACSHA256.HashData(secret.Material.Span, signedPayload);
        return SignatureScheme + "=" + Convert.ToHexStringLower(mac);
    }

    /// <summary>
    /// Receiver-side verification (published to partners; also exercised by
    /// tests): constant-time signature comparison and timestamp bound.
    /// </summary>
    public static bool Verify(
        WebhookSigningSecret secret,
        long unixTimestampSeconds,
        string body,
        string signature,
        DateTimeOffset now,
        TimeSpan? replayTolerance = null)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(body);

        if (string.IsNullOrEmpty(signature))
        {
            return false;
        }

        var tolerance = replayTolerance ?? DefaultReplayTolerance;
        if (Math.Abs(now.ToUnixTimeSeconds() - unixTimestampSeconds) > (long)tolerance.TotalSeconds)
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(Sign(secret, unixTimestampSeconds, body));
        var presented = Encoding.UTF8.GetBytes(signature);
        return CryptographicOperations.FixedTimeEquals(expected, presented);
    }
}
