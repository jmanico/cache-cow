using System.Net;
using System.Net.Sockets;

namespace CacheCow.Modules.WholesaleB2B.Webhooks;

/// <summary>
/// A partner-supplied webhook receiver URL failed the SSRF policy. The message
/// is generic by design: it never echoes the submitted URL (which could embed
/// credentials) into error bodies or logs (SECURITY.md, Logging rules 1, 4).
/// </summary>
public sealed class WebhookUrlRejectedException : WholesaleException
{
    public WebhookUrlRejectedException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// DNS resolution port for the SSRF policy (SECURITY.md, Input validation
/// rule 8). The host adapts real DNS resolution; the module validates every
/// resolved address, not just the hostname string, so a public-looking name
/// resolving to a private address is rejected. Contract: return every address
/// the name resolves to; throw or return empty when resolution fails — the
/// caller fails closed either way.
/// </summary>
public interface IWebhookAddressResolver
{
    IReadOnlyList<IPAddress> Resolve(string hostName);
}

/// <summary>
/// SSRF policy for partner webhook receiver URLs (CC-API-009; SECURITY.md,
/// Input validation rule 8): HTTPS only, no embedded credentials, and every
/// address the host resolves to must be public — internal, loopback,
/// link-local, private-range, unique-local, multicast, unspecified, and
/// cloud-metadata addresses are rejected in IPv4, IPv6, IPv4-mapped-IPv6, and
/// NAT64 forms.
///
/// DNS-rebinding contract: this validation runs at REGISTRATION and again at
/// DELIVERY time (see <see cref="WebhookDeliveryService"/>), and the delivery
/// transport (<see cref="IWebhookDeliveryTransport"/>) must additionally pin
/// the connection to an address it re-validated — a name that flips to a
/// private address between check and connect must still be refused. Failure
/// anywhere (resolver exception, empty resolution) is a rejection: no
/// outbound request is ever made to an unvalidated destination (SECURITY.md,
/// Logging rule 2).
/// </summary>
public static class WebhookUrlValidator
{
    /// <summary>Throws <see cref="WebhookUrlRejectedException"/> unless <paramref name="url"/> passes the full policy.</summary>
    public static void EnsureDeliverable(Uri url, IWebhookAddressResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(resolver);

        if (!url.IsAbsoluteUri)
        {
            throw new WebhookUrlRejectedException("Webhook receiver URLs must be absolute (CC-API-009).");
        }

        if (!string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            throw new WebhookUrlRejectedException("Webhook receiver URLs must use https (CC-API-009; AC-01).");
        }

        if (!string.IsNullOrEmpty(url.UserInfo))
        {
            throw new WebhookUrlRejectedException(
                "Webhook receiver URLs must not embed credentials (SECURITY.md, Logging rule 4).");
        }

        var host = url.IdnHost;
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new WebhookUrlRejectedException("Webhook receiver URLs require a host (CC-API-009).");
        }

        if (IPAddress.TryParse(host, out var literal))
        {
            EnsurePublic(literal);
            return;
        }

        IReadOnlyList<IPAddress> addresses;
        try
        {
            addresses = resolver.Resolve(host);
        }
        catch (Exception)
        {
            // Fail closed: unresolvable is undeliverable (SECURITY.md, Logging rule 2).
            throw new WebhookUrlRejectedException("Webhook receiver host could not be validated (CC-API-009).");
        }

        if (addresses is not { Count: > 0 })
        {
            throw new WebhookUrlRejectedException("Webhook receiver host could not be validated (CC-API-009).");
        }

        foreach (var address in addresses)
        {
            EnsurePublic(address);
        }
    }

    private static void EnsurePublic(IPAddress address)
    {
        if (IsBlockedAddress(address))
        {
            throw new WebhookUrlRejectedException(
                "Webhook receiver addresses must be public (SECURITY.md, Input validation rule 8).");
        }
    }

    /// <summary>
    /// True for every address class the SSRF policy blocks. IPv4-mapped IPv6
    /// (<c>::ffff:a.b.c.d</c>) and NAT64 (<c>64:ff9b::/96</c>) encodings are
    /// unwrapped and judged as their embedded IPv4 address, so encoding tricks
    /// cannot smuggle a private target past the check.
    /// </summary>
    public static bool IsBlockedAddress(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.IsIPv4MappedToIPv6)
        {
            return IsBlockedAddress(address.MapToIPv4());
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return b[0] == 0 // 0.0.0.0/8 ("this network")
                || b[0] == 10 // 10.0.0.0/8 private
                || b[0] == 127 // 127.0.0.0/8 loopback
                || (b[0] == 100 && (b[1] & 0xC0) == 64) // 100.64.0.0/10 CGNAT
                || (b[0] == 169 && b[1] == 254) // 169.254.0.0/16 link-local incl. 169.254.169.254 metadata
                || (b[0] == 172 && (b[1] & 0xF0) == 16) // 172.16.0.0/12 private
                || (b[0] == 192 && b[1] == 0 && b[2] == 0) // 192.0.0.0/24 IETF protocol assignments
                || (b[0] == 192 && b[1] == 168) // 192.168.0.0/16 private
                || (b[0] == 198 && (b[1] & 0xFE) == 18) // 198.18.0.0/15 benchmarking
                || b[0] >= 224; // 224.0.0.0/3 multicast, reserved, broadcast
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (IPAddress.IsLoopback(address)
                || address.Equals(IPAddress.IPv6Any) // :: unspecified
                || address.IsIPv6LinkLocal // fe80::/10
                || address.IsIPv6SiteLocal // fec0::/10 (deprecated, still blocked)
                || address.IsIPv6UniqueLocal // fc00::/7
                || address.IsIPv6Multicast) // ff00::/8
            {
                return true;
            }

            var bytes = address.GetAddressBytes();

            // NAT64 64:ff9b::/96 — judge the embedded IPv4 address.
            if (bytes[0] == 0x00 && bytes[1] == 0x64 && bytes[2] == 0xff && bytes[3] == 0x9b)
            {
                return IsBlockedAddress(new IPAddress(bytes[12..16]));
            }

            return false;
        }

        // Unknown address family: fail closed.
        return true;
    }
}
