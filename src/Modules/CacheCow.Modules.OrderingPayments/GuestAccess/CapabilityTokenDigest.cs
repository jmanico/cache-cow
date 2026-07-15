using System.Security.Cryptography;
using System.Text;

namespace CacheCow.Modules.OrderingPayments.GuestAccess;

/// <summary>
/// SHA-256 digest of a capability-token secret (issue 042). The digest — not
/// the secret — is what the store persists and what lookups key on, so a
/// database read can never yield a usable token (SECURITY.md, Authentication
/// rule 14: tokens are secrets). The digest itself is not secret material and
/// is safe as a storage key, but it is still kept out of user-facing output.
/// </summary>
public readonly record struct CapabilityTokenDigest
{
    private readonly string? _hex;

    private CapabilityTokenDigest(string hex)
    {
        _hex = hex;
    }

    /// <summary>Uppercase hex SHA-256 digest.</summary>
    public string Hex =>
        _hex ?? throw new InvalidOperationException(
            "Uninitialized CapabilityTokenDigest; obtain one from Compute.");

    /// <summary>Digest of the token secret's UTF-8 bytes.</summary>
    public static CapabilityTokenDigest Compute(string tokenSecret)
    {
        ArgumentException.ThrowIfNullOrEmpty(tokenSecret);
        return new CapabilityTokenDigest(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tokenSecret))));
    }

    /// <summary>
    /// Constant-time digest comparison (issue 042: token validation compares
    /// hashes in constant time, so equality checking never leaks how many
    /// leading bytes matched).
    /// </summary>
    public bool FixedTimeEquals(CapabilityTokenDigest other) =>
        CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(Hex),
            Convert.FromHexString(other.Hex));

    public override string ToString() => Hex;
}
