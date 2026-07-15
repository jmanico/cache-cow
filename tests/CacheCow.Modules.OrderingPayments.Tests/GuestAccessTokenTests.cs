using System.Buffers.Text;
using System.Reflection;
using CacheCow.Modules.OrderingPayments.GuestAccess;
using CacheCow.Modules.OrderingPayments.Orders;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.OrderingPayments.Tests;

/// <summary>
/// Issue 042 (CC-ORD-010, CC-SEC-017, CC-INV-002): guest capability tokens —
/// ≥128 bits of CSPRNG entropy, bound to exactly one order and one purpose,
/// expiring, server-revocable, stored only as a SHA-256 digest, validated in
/// constant time, redacted from formatting, with all failure modes
/// indistinguishable to the caller.
/// </summary>
[Requirement("CC-ORD-010")]
[Requirement("CC-SEC-017")]
public sealed class GuestAccessTokenTests
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30); // fixture value; the real duration is an open decision (issue 042)

    private static GuestAccessTokenService Service(
        out InMemoryCapabilityTokenStore store,
        out ManualTimeProvider clock)
    {
        store = new InMemoryCapabilityTokenStore();
        clock = new ManualTimeProvider(Fixtures.T0);
        return new GuestAccessTokenService(store, new GuestAccessOptions(Lifetime), clock);
    }

    [Fact]
    public void Round_trip_valid_token_authorizes_exactly_its_order_and_purpose()
    {
        // AC-01/AC-02: a valid, unexpired, unrevoked token resolves the one
        // order it is bound to.
        var service = Service(out _, out _);
        var orderId = OrderId.New();
        var token = service.Issue(orderId, GuestAccessPurpose.OrderStatus);

        Assert.True(service.TryAuthorize(token.RevealSecret(), GuestAccessPurpose.OrderStatus, out var resolved));
        Assert.Equal(orderId, resolved);
        Assert.True(service.IsAuthorizedFor(token.RevealSecret(), GuestAccessPurpose.OrderStatus, orderId));
    }

    [Fact]
    public void Token_secret_has_at_least_128_bits_of_crypto_rng_entropy_and_is_url_safe()
    {
        // AC-01: 32 CSPRNG bytes (256 bits), well above the 128-bit floor,
        // base64url-encoded (opaque, URL-safe: no '+', '/', '=', no padding).
        var service = Service(out _, out _);
        var secret = service.Issue(OrderId.New(), GuestAccessPurpose.OrderStatus).RevealSecret();

        var decoded = Base64Url.DecodeFromChars(secret);
        Assert.Equal(GuestAccessTokenService.SecretByteLength, decoded.Length);
        Assert.True(decoded.Length * 8 >= 128);
        Assert.Matches("^[A-Za-z0-9_-]+$", secret);
    }

    [Fact]
    public void Issued_tokens_are_unique()
    {
        var service = Service(out _, out _);
        var orderId = OrderId.New();

        var secrets = Enumerable.Range(0, 100)
            .Select(_ => service.Issue(orderId, GuestAccessPurpose.OrderTracking).RevealSecret())
            .ToArray();

        Assert.Equal(secrets.Length, secrets.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Token_bound_to_order_A_is_not_valid_for_order_B()
    {
        // AC-04: cross-order presentation is simply not valid (the endpoint
        // answers 404, revealing nothing about order B).
        var service = Service(out _, out _);
        var orderA = OrderId.New();
        var orderB = OrderId.New();
        var token = service.Issue(orderA, GuestAccessPurpose.OrderStatus);

        Assert.False(service.IsAuthorizedFor(token.RevealSecret(), GuestAccessPurpose.OrderStatus, orderB));
    }

    [Fact]
    public void Token_is_single_purpose()
    {
        // A status token opens neither tracking nor invoice download.
        var service = Service(out _, out _);
        var token = service.Issue(OrderId.New(), GuestAccessPurpose.OrderStatus);

        Assert.False(service.TryAuthorize(token.RevealSecret(), GuestAccessPurpose.OrderTracking, out _));
        Assert.False(service.TryAuthorize(token.RevealSecret(), GuestAccessPurpose.InvoiceDownload, out _));
    }

    [Fact]
    public void Expired_token_is_denied()
    {
        // AC-05: expiry is enforced against the server clock, inclusive at
        // the boundary.
        var service = Service(out _, out var clock);
        var token = service.Issue(OrderId.New(), GuestAccessPurpose.OrderStatus);

        clock.Advance(Lifetime - TimeSpan.FromSeconds(1));
        Assert.True(service.TryAuthorize(token.RevealSecret(), GuestAccessPurpose.OrderStatus, out _));

        clock.Advance(TimeSpan.FromSeconds(1)); // exactly ExpiresAt
        Assert.False(service.TryAuthorize(token.RevealSecret(), GuestAccessPurpose.OrderStatus, out _));
    }

    [Fact]
    public void Revoked_token_is_denied_by_digest_and_by_order()
    {
        // AC-01/AC-05: server-side revocation needs no secret.
        var service = Service(out _, out _);
        var orderId = OrderId.New();

        var byDigest = service.Issue(orderId, GuestAccessPurpose.OrderStatus);
        service.Revoke(byDigest.Digest);
        Assert.False(service.TryAuthorize(byDigest.RevealSecret(), GuestAccessPurpose.OrderStatus, out _));

        var byOrder = service.Issue(orderId, GuestAccessPurpose.InvoiceDownload);
        var otherOrder = service.Issue(OrderId.New(), GuestAccessPurpose.InvoiceDownload);
        service.RevokeAllForOrder(orderId);
        Assert.False(service.TryAuthorize(byOrder.RevealSecret(), GuestAccessPurpose.InvoiceDownload, out _));
        Assert.True(service.TryAuthorize(otherOrder.RevealSecret(), GuestAccessPurpose.InvoiceDownload, out _));
    }

    [Fact]
    public void Expired_revoked_unknown_and_wrong_purpose_are_indistinguishable()
    {
        // Failure modes yield the identical NotValid outcome — no oracle for
        // an attacker probing which tokens exist (issue 042, Failure Behavior).
        var service = Service(out _, out var clock);
        var orderId = OrderId.New();

        var expired = service.Issue(orderId, GuestAccessPurpose.OrderStatus);
        clock.Advance(Lifetime + TimeSpan.FromSeconds(1));
        var revoked = service.Issue(orderId, GuestAccessPurpose.OrderStatus);
        service.Revoke(revoked.Digest);
        var wrongPurpose = service.Issue(orderId, GuestAccessPurpose.OrderTracking);

        var outcomes = new[]
        {
            (service.TryAuthorize(expired.RevealSecret(), GuestAccessPurpose.OrderStatus, out var o1), o1),
            (service.TryAuthorize(revoked.RevealSecret(), GuestAccessPurpose.OrderStatus, out var o2), o2),
            (service.TryAuthorize(wrongPurpose.RevealSecret(), GuestAccessPurpose.OrderStatus, out var o3), o3),
            (service.TryAuthorize(Base64Url.EncodeToString(new byte[32]), GuestAccessPurpose.OrderStatus, out var o4), o4),
        };

        Assert.All(outcomes, outcome =>
        {
            Assert.False(outcome.Item1);
            Assert.Equal(default, outcome.Item2); // no order leaks on any failure
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")] // too short: below 128 bits
    [InlineData("not base64url !!!")]
    [InlineData("++++++++++++++++++++++++++++++++++++++++++++")] // base64, not base64url
    public void Malformed_presented_tokens_are_denied_without_exceptions(string? presented)
    {
        var service = Service(out _, out _);

        Assert.False(service.TryAuthorize(presented, GuestAccessPurpose.OrderStatus, out var orderId));
        Assert.Equal(default, orderId);
    }

    [Fact]
    public void Oversized_presented_token_is_denied()
    {
        var service = Service(out _, out _);
        var oversized = new string('A', 4096);

        Assert.False(service.TryAuthorize(oversized, GuestAccessPurpose.OrderStatus, out _));
    }

    [Fact]
    public void Store_failure_is_a_denial_never_a_bypass()
    {
        // Fail closed (issue 042, Failure Behavior; SECURITY.md, Logging rule 2).
        var service = new GuestAccessTokenService(
            new ThrowingCapabilityTokenStore(),
            new GuestAccessOptions(Lifetime),
            new ManualTimeProvider(Fixtures.T0));
        var validLookingSecret = new string('A', 43);

        Assert.False(service.TryAuthorize(validLookingSecret, GuestAccessPurpose.OrderStatus, out _));
    }

    [Fact]
    public void Store_holds_only_the_digest_never_the_secret()
    {
        // The persisted record has no secret field at all; the digest is not
        // the secret and does not contain it.
        var service = Service(out var store, out _);
        var token = service.Issue(OrderId.New(), GuestAccessPurpose.InvoiceDownload);
        var secret = token.RevealSecret();

        var record = store.Find(token.Digest);
        Assert.NotNull(record);
        Assert.DoesNotContain(
            typeof(CapabilityTokenRecord).GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.PropertyType == typeof(string));
        Assert.DoesNotContain(secret, record.Digest.Hex, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Digest_comparison_is_available_in_constant_time_form()
    {
        // Validation compares stored vs presented digests via
        // CryptographicOperations.FixedTimeEquals (wrapped here).
        var a = CapabilityTokenDigest.Compute("token-a");
        var same = CapabilityTokenDigest.Compute("token-a");
        var other = CapabilityTokenDigest.Compute("token-b");

        Assert.True(a.FixedTimeEquals(same));
        Assert.False(a.FixedTimeEquals(other));
    }

    [Fact]
    public void ToString_redacts_the_secret_everywhere()
    {
        // AC-06: the secret never leaks through formatting into logs or
        // telemetry (SECURITY.md, Logging rule 4).
        var service = Service(out _, out _);
        var token = service.Issue(OrderId.New(), GuestAccessPurpose.OrderStatus);
        var secret = token.RevealSecret();

        Assert.DoesNotContain(secret, token.ToString(), StringComparison.Ordinal);
        Assert.Contains("redacted", token.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(secret, token.Digest.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Secret_is_exposed_via_method_not_property_so_serializers_skip_it()
    {
        // No public property or field of CapabilityToken yields the secret.
        var service = Service(out _, out _);
        var token = service.Issue(OrderId.New(), GuestAccessPurpose.OrderStatus);
        var secret = token.RevealSecret();

        foreach (var property in typeof(CapabilityToken).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.NotEqual(secret, property.GetValue(token) as string);
        }

        Assert.Empty(typeof(CapabilityToken).GetFields(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void No_order_number_plus_email_lookup_path_exists()
    {
        // AC-03, structurally: the service exposes no member taking an email
        // or an order-number string — resolution goes from token to order only.
        var members = typeof(GuestAccessTokenService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name.Contains("Email", StringComparison.OrdinalIgnoreCase)
                || m.GetParameters().Any(p => p.Name!.Contains("email", StringComparison.OrdinalIgnoreCase)
                    || p.Name.Contains("orderNumber", StringComparison.OrdinalIgnoreCase)));

        Assert.Empty(members);
    }

    [Fact]
    [Requirement("CC-INV-002")]
    public void Invoice_access_resolves_from_token_alone_never_a_guessable_order_identifier()
    {
        // AC-08: the guest invoice link needs only the token — the order is
        // resolved FROM it, so no order identifier is the access key.
        var service = Service(out _, out _);
        var orderId = OrderId.New();
        var token = service.Issue(orderId, GuestAccessPurpose.InvoiceDownload);

        Assert.True(service.TryAuthorize(token.RevealSecret(), GuestAccessPurpose.InvoiceDownload, out var resolved));
        Assert.Equal(orderId, resolved);
        Assert.DoesNotContain(orderId.ToString(), token.RevealSecret(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Options_require_a_positive_lifetime_no_default_exists()
    {
        // The token lifetime is an open decision (issue 042): required
        // config, no default, non-positive rejected.
        Assert.Throws<ArgumentOutOfRangeException>(() => new GuestAccessOptions(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GuestAccessOptions(TimeSpan.FromDays(-1)));
        Assert.Null(typeof(GuestAccessOptions).GetConstructor(Type.EmptyTypes));
    }
}
