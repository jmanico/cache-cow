using System.Security.Cryptography;

namespace CacheCow.Modules.OrderingPayments.Idempotency;

/// <summary>
/// Digest binding an idempotency key to the request that first used it
/// (CC-SEC-015; SECURITY.md, Input validation rule 12). Same key + matching
/// fingerprint = replay of the stored result; same key + different fingerprint
/// = 409 conflict, never the original result and never a new order.
/// </summary>
public readonly record struct RequestFingerprint
{
    private readonly string? _value;

    private RequestFingerprint(string value)
    {
        _value = value;
    }

    /// <summary>Uppercase hex digest.</summary>
    public string Value =>
        _value ?? throw new InvalidOperationException(
            "Uninitialized RequestFingerprint; obtain one from an IRequestFingerprintStrategy.");

    public static RequestFingerprint FromHexDigest(string hexDigest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hexDigest);
        return new RequestFingerprint(hexDigest.ToUpperInvariant());
    }

    public override string ToString() => Value;
}

/// <summary>
/// Injectable fingerprint construction (issue 037). OPEN DECISION: the specs
/// mandate the key-to-request binding but not the algorithm — which request
/// parts participate (raw body bytes vs. canonicalized body; whether
/// method/path/headers are included) and the hash primitive are unratified
/// (issue 037, Open Questions). Callers pass the bytes they consider the
/// canonical request content — per the issue's constraints, the received
/// request content before any mutation — and the strategy digests exactly
/// what it is given.
/// </summary>
public interface IRequestFingerprintStrategy
{
    RequestFingerprint ComputeFingerprint(ReadOnlySpan<byte> requestContent);
}

/// <summary>
/// First-party default: SHA-256 over the caller-supplied canonical request
/// bytes. Deterministic and collision-resistant; carries no secret (a
/// fingerprint is an equality check inside one scope, not an authenticator —
/// access control is the (scope, key) lookup, CC-SEC-015).
/// </summary>
public sealed class Sha256RequestFingerprintStrategy : IRequestFingerprintStrategy
{
    public RequestFingerprint ComputeFingerprint(ReadOnlySpan<byte> requestContent) =>
        RequestFingerprint.FromHexDigest(Convert.ToHexString(SHA256.HashData(requestContent)));
}
